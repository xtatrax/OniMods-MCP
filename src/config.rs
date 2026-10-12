use anyhow::{Context, Result};
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::env;
use std::path::{Path, PathBuf};
#[cfg(target_os = "windows")]
use std::process::Command;

const DEFAULT_CONFIG_NAME: &str = "onim.toml";
const BUILD_PROPS: &str = "Directory.Build.props";

/// 個別の Mod 設定。
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ModConfig {
    pub path: PathBuf,
    pub name: Option<String>,
    /// Steam ワークショップ ID（publishedfileid）。設定すると公開時に自動で使用する。
    pub publishedfileid: Option<String>,
    /// 任意の Steam ワークショップタイトル。未設定時は mod.yaml の title を使用する。
    pub workshop_title: Option<String>,
}

impl ModConfig {
    pub fn project_abs(&self, repo_root: &Path) -> PathBuf {
        if self.path.is_absolute() {
            self.path.clone()
        } else {
            repo_root.join(&self.path)
        }
    }
    pub fn mod_name(&self, key: &str) -> String {
        self.name.clone().unwrap_or_else(|| key.to_string())
    }
}

#[derive(Debug, Serialize, Deserialize)]
pub struct Config {
    #[serde(skip)]
    pub game_path: PathBuf,
    #[serde(default)]
    pub mods: HashMap<String, ModConfig>,
    pub default_mod: Option<String>,
}

/// `onim doctor` 用の、問題があっても処理を中断しない設定診断の結果。
#[derive(Debug)]
pub struct DoctorConfigDiagnostics {
    pub repo_root: PathBuf,
    pub config_path: PathBuf,
    pub config_error: Option<String>,
    pub mods: Option<HashMap<String, ModConfig>>,
    pub props_path: PathBuf,
    pub game_path_error: Option<String>,
    pub game_path: Option<PathBuf>,
    pub managed_path: Option<PathBuf>,
    pub game_mods_dir_error: Option<String>,
    pub game_mods_dir: Option<PathBuf>,
}

#[derive(Debug)]
pub struct SelectedMod {
    pub name: String,
    pub config: ModConfig,
}

impl SelectedMod {
    /// csproj ファイルから AssemblyName を読み取る。
    /// 見つからない場合は name を PascalCase に変換して返す。
    pub fn assembly_name(&self, repo_root: &std::path::Path) -> String {
        let project_dir = self.config.project_abs(repo_root);
        let csproj = project_dir.read_dir().ok().and_then(|mut entries| {
            entries.find_map(|e| {
                let e = e.ok()?;
                let name = e.file_name().into_string().ok()?;
                if name.ends_with(".csproj") {
                    Some(e.path())
                } else {
                    None
                }
            })
        });

        if let Some(csproj) = csproj
            && let Ok(content) = std::fs::read_to_string(&csproj)
        {
            // <AssemblyName>値</AssemblyName> を簡易解析する。
            if let Some(start) = content.find("<AssemblyName>")
                && let Some(end) = content.find("</AssemblyName>")
                && end > start
            {
                let val = &content[start + "<AssemblyName>".len()..end];
                let trimmed = val.trim();
                if !trimmed.is_empty() {
                    return trimmed.to_string();
                }
            }
        }

        // フォールバックとして snake_case を PascalCase に変換する。
        to_pascal_case(&self.name)
    }
}

fn to_pascal_case(s: &str) -> String {
    s.split('_')
        .map(|word| {
            let mut chars = word.chars();
            match chars.next() {
                None => String::new(),
                Some(first) => {
                    first.to_uppercase().collect::<String>() + &chars.as_str().to_lowercase()
                }
            }
        })
        .collect()
}

/// Directory.Build.props からゲームのパスを読み取る。
fn read_game_path_from_props(repo_root: &Path) -> Result<PathBuf> {
    let props = repo_root.join(BUILD_PROPS);
    if !props.exists() {
        anyhow::bail!(
            "{} が見つかりません。先に `onim setup` でプロジェクトを初期設定してください",
            BUILD_PROPS
        );
    }
    let content = std::fs::read_to_string(&props)
        .with_context(|| format!("{} の読み取りに失敗しました", props.display()))?;

    parse_game_path_from_props(&content)
}

fn parse_game_path_from_props(content: &str) -> Result<PathBuf> {
    // <OniGamePath>値</OniGamePath> を簡易解析する。
    let start = content.find("<OniGamePath>");
    let end = content.find("</OniGamePath>");
    match (start, end) {
        (Some(s), Some(e)) if e > s => {
            let val = &content[s + "<OniGamePath>".len()..e];
            let trimmed = val.trim();
            if trimmed.is_empty() {
                anyhow::bail!("{} の OniGamePath が空です", BUILD_PROPS);
            }
            Ok(PathBuf::from(trimmed))
        }
        _ => anyhow::bail!(
            "{} に <OniGamePath> タグが見つかりません。先に `onim setup` を実行してください",
            BUILD_PROPS
        ),
    }
}

/// 通常のコマンドで行うパス検証を省略し、`onim doctor` の設定診断情報を読み取る。
pub fn read_doctor_diagnostics(explicit_path: Option<PathBuf>) -> Result<DoctorConfigDiagnostics> {
    let (config_path, repo_root) = resolve_doctor_config_location(explicit_path)?;
    let (mods, config_error) = inspect_config_for_doctor(&config_path);
    let props_path = repo_root.join(BUILD_PROPS);
    let (game_path, game_path_error) = inspect_game_path_for_doctor(&props_path);
    let managed_path = game_path.as_ref().map(|path| managed_path_for_game(path));
    let (game_mods_dir, game_mods_dir_error) = match game_user_data_dir() {
        Ok(base) => (Some(base.join("mods")), None),
        Err(error) => (None, Some(error.to_string())),
    };

    Ok(DoctorConfigDiagnostics {
        repo_root,
        config_path,
        config_error,
        mods,
        props_path,
        game_path_error,
        game_path,
        managed_path,
        game_mods_dir_error,
        game_mods_dir,
    })
}

fn resolve_doctor_config_location(explicit_path: Option<PathBuf>) -> Result<(PathBuf, PathBuf)> {
    let current_dir = env::current_dir().context("現在の作業ディレクトリを取得できません")?;
    let config_path = match explicit_path {
        Some(path) if path.is_absolute() => path,
        Some(path) => current_dir.join(path),
        None => find_config_file()?.unwrap_or_else(|| current_dir.join(DEFAULT_CONFIG_NAME)),
    };
    let repo_root = config_path
        .parent()
        .map(Path::to_path_buf)
        .context("onim 設定ファイルのあるディレクトリを特定できません")?;
    Ok((config_path, repo_root))
}

fn inspect_config_for_doctor(
    config_path: &Path,
) -> (Option<HashMap<String, ModConfig>>, Option<String>) {
    let content = match std::fs::read_to_string(config_path) {
        Ok(content) => content,
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => {
            return (
                None,
                Some(format!(
                    "設定ファイルが存在しません: {}",
                    config_path.display()
                )),
            );
        }
        Err(error) => {
            return (
                None,
                Some(format!(
                    "設定ファイルの読み取りに失敗しました: {}（{error}）",
                    config_path.display()
                )),
            );
        }
    };

    match parse_config_for_doctor(&content) {
        Ok(mods) => (Some(mods), None),
        Err(error) => (
            None,
            Some(format!(
                "設定ファイルの解析に失敗しました: {}（{error}）",
                config_path.display()
            )),
        ),
    }
}

fn parse_config_for_doctor(content: &str) -> Result<HashMap<String, ModConfig>> {
    Ok(toml::from_str::<Config>(content)?.mods)
}

fn inspect_game_path_for_doctor(props_path: &Path) -> (Option<PathBuf>, Option<String>) {
    let content = match std::fs::read_to_string(props_path) {
        Ok(content) => content,
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => {
            return (
                None,
                Some(format!("{} が見つかりません", props_path.display())),
            );
        }
        Err(error) => {
            return (
                None,
                Some(format!(
                    "{} の読み取りに失敗しました: {error}",
                    props_path.display()
                )),
            );
        }
    };

    match parse_game_path_from_props(&content) {
        Ok(path) => (Some(path), None),
        Err(error) => (None, Some(error.to_string())),
    }
}

fn managed_path_for_game(game_path: &Path) -> PathBuf {
    #[cfg(target_os = "windows")]
    {
        game_path.join("OxygenNotIncluded_Data").join("Managed")
    }
    #[cfg(not(target_os = "windows"))]
    {
        let mac_managed = game_path
            .join("OxygenNotIncluded.app")
            .join("Contents")
            .join("OxygenNotIncluded_Data")
            .join("Managed");
        if mac_managed.exists() {
            mac_managed
        } else {
            game_path.join("OxygenNotIncluded_Data").join("Managed")
        }
    }
}

impl Config {
    pub fn select_all_mods(&self) -> Result<Vec<SelectedMod>> {
        if self.mods.is_empty() {
            anyhow::bail!(
                "Mod が設定されていません。{} に [mods.XXX] を追加してください",
                DEFAULT_CONFIG_NAME
            );
        }

        let mut keys: Vec<_> = self.mods.keys().cloned().collect();
        keys.sort();

        let mut result = Vec::with_capacity(keys.len());
        for key in keys {
            let cfg = self
                .mods
                .get(&key)
                .with_context(|| format!("設定が見つかりません: {}", key))?
                .clone();
            result.push(SelectedMod {
                name: cfg.mod_name(&key),
                config: cfg,
            });
        }
        Ok(result)
    }

    pub fn select_mod(&self, explicit: Option<String>) -> Result<SelectedMod> {
        if self.mods.is_empty() {
            anyhow::bail!(
                "Mod が設定されていません。{} に [mods.XXX] を追加してください",
                DEFAULT_CONFIG_NAME
            );
        }
        // HashMap iteration order is deliberately unspecified. Use the same
        // alphabetical fallback as `select_all_mods` so a missing default is
        // deterministic across runs and platforms.
        let fallback = self
            .mods
            .keys()
            .min()
            .cloned()
            .ok_or_else(|| anyhow::anyhow!("選択できる Mod がありません"))?;
        let key = match explicit {
            Some(k) => {
                if !self.mods.contains_key(&k) {
                    anyhow::bail!(
                        "Mod '{}' が見つかりません。設定済みの Mod: {}\n-m で正しい名前を指定してください。",
                        k,
                        self.mods.keys().cloned().collect::<Vec<_>>().join(", ")
                    );
                }
                k
            }
            None => self
                .default_mod
                .as_ref()
                .filter(|default| self.mods.contains_key(*default))
                .cloned()
                .unwrap_or(fallback),
        };
        let cfg = self
            .mods
            .get(&key)
            .cloned()
            .ok_or_else(|| anyhow::anyhow!("選択した Mod が見つかりません: {}", key))?;
        Ok(SelectedMod {
            name: cfg.mod_name(&key),
            config: cfg,
        })
    }

    pub fn managed_path(&self) -> PathBuf {
        managed_path_for_game(&self.game_path)
    }

    pub fn game_mods_dir(&self) -> Result<PathBuf> {
        let base = game_user_data_dir()?;
        Ok(base.join("mods"))
    }

    pub fn dev_mod_dir(&self, mod_name: &str) -> Result<PathBuf> {
        Ok(self.game_mods_dir()?.join("Dev").join(mod_name))
    }

    pub fn legacy_dev_mod_dir(&self, mod_name: &str) -> Result<PathBuf> {
        Ok(self.game_mods_dir()?.join("dev").join(mod_name))
    }

    pub fn local_mod_dir(&self, mod_name: &str) -> Result<PathBuf> {
        Ok(self.game_mods_dir()?.join("Local").join(mod_name))
    }

    pub fn dist_dir(&self, repo_root: &Path) -> PathBuf {
        repo_root.join("dist")
    }

    pub fn validate(&self) -> Result<()> {
        if !self.game_path.exists() {
            anyhow::bail!(
                "ゲームのパスが存在しません: {}\n`onim setup` を実行して設定し直してください",
                self.game_path.display()
            );
        }
        let managed = self.managed_path();
        if !managed.join("Assembly-CSharp.dll").exists() {
            anyhow::bail!(
                "Assembly-CSharp.dll が見つかりません。ゲームのパスが正しくない可能性があります: {}\n想定される場所: {}\n`onim setup` を実行して設定し直してください",
                self.game_path.display(),
                managed.display()
            );
        }
        Ok(())
    }
}

pub fn load(explicit_path: Option<PathBuf>) -> Result<Config> {
    let config_path = if let Some(p) = explicit_path {
        p
    } else {
        find_config_file()?.unwrap_or_else(|| PathBuf::from(DEFAULT_CONFIG_NAME))
    };

    let repo_root = match config_path.parent() {
        Some(parent) => parent.to_path_buf(),
        None => env::current_dir().context("現在のディレクトリの取得に失敗しました")?,
    };

    // Directory.Build.props からゲームのパスを読み取る。
    let game_path = read_game_path_from_props(&repo_root)?;

    let cfg: Config = if config_path.exists() {
        let content = std::fs::read_to_string(&config_path).with_context(|| {
            format!(
                "設定ファイルの読み取りに失敗しました: {}",
                config_path.display()
            )
        })?;
        let mut parsed: Config = toml::from_str(&content).with_context(|| {
            format!(
                "設定ファイルの解析に失敗しました: {}",
                config_path.display()
            )
        })?;
        parsed.game_path = game_path;
        parsed
    } else {
        Config {
            game_path,
            mods: HashMap::new(),
            default_mod: None,
        }
    };

    for (key, m) in &cfg.mods {
        let abs = m.project_abs(&repo_root);
        if !abs.exists() {
            anyhow::bail!(
                "Mod '{}' のパスが存在しません: {}\n{} の [mods.{}] にある path を確認してください",
                key,
                abs.display(),
                config_path.display(),
                key
            );
        }
    }

    unsafe {
        env::set_var("ONI_CLI_REPO_ROOT", repo_root.as_os_str());
    }

    cfg.validate()?;
    Ok(cfg)
}

fn find_config_file() -> Result<Option<PathBuf>> {
    let mut dir = env::current_dir()?;
    loop {
        let candidate = dir.join(DEFAULT_CONFIG_NAME);
        if candidate.exists() {
            return Ok(Some(candidate));
        }
        if !dir.pop() {
            break;
        }
    }
    Ok(None)
}

fn game_user_data_dir() -> Result<PathBuf> {
    #[cfg(target_os = "linux")]
    {
        let home = env::var_os("HOME").context("環境変数 HOME を取得できません")?;
        Ok(PathBuf::from(home).join(".config/unity3d/Klei/Oxygen Not Included"))
    }

    #[cfg(target_os = "windows")]
    {
        // ONI loads Steam/Dev/Local mods from Documents\Klei\OxygenNotIncluded\mods.
        // LocalLow\...\Oxygen Not Included holds logs/config for some installs, not the
        // mod folders used by this Windows layout (see Player.log Loaded assembly path).
        let output = Command::new("powershell")
            .args([
                "-NoProfile",
                "-NonInteractive",
                "-Command",
                "[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false); [Environment]::GetFolderPath([Environment+SpecialFolder]::MyDocuments)",
            ])
            .output()
            .context("Windows Known Folder API から Documents ディレクトリを取得できません")?;
        if !output.status.success() {
            let stderr = String::from_utf8_lossy(&output.stderr);
            anyhow::bail!(
                "Windows Known Folder API からの Documents ディレクトリ取得に失敗しました（{}）: {}",
                output.status,
                stderr.trim()
            );
        }

        let documents = String::from_utf8(output.stdout).context(
            "Windows Known Folder API が返した Documents ディレクトリは有効な UTF-8 ではありません",
        )?;
        let documents = documents.trim();
        if documents.is_empty() {
            anyhow::bail!("Windows Known Folder API が空の Documents ディレクトリを返しました");
        }
        Ok(PathBuf::from(documents).join("Klei/OxygenNotIncluded"))
    }

    #[cfg(target_os = "macos")]
    {
        let home = env::var_os("HOME").context("環境変数 HOME を取得できません")?;
        Ok(PathBuf::from(home).join("Library/Application Support/unity.Klei.Oxygen Not Included"))
    }
}

#[cfg(test)]
mod tests {
    use super::{Config, ModConfig, parse_config_for_doctor};
    use std::collections::HashMap;
    use std::path::{Path, PathBuf};

    #[test]
    fn doctor_config_parser_should_preserve_resolvable_mods() -> anyhow::Result<()> {
        let mods = parse_config_for_doctor(
            r#"
default_mod = "Example"

[mods.Example]
path = "mods/Example"
"#,
        )?;

        assert_eq!(
            mods["Example"].project_abs(Path::new("/repo")),
            Path::new("/repo/mods/Example")
        );
        Ok(())
    }

    #[test]
    fn select_mod_without_default_uses_sorted_name() -> anyhow::Result<()> {
        let mods = HashMap::from([
            (
                "Zulu".to_string(),
                ModConfig {
                    path: PathBuf::from("mods/Zulu"),
                    name: None,
                    publishedfileid: None,
                    workshop_title: None,
                },
            ),
            (
                "Alpha".to_string(),
                ModConfig {
                    path: PathBuf::from("mods/Alpha"),
                    name: None,
                    publishedfileid: None,
                    workshop_title: None,
                },
            ),
        ]);
        let config = Config {
            game_path: PathBuf::new(),
            mods,
            default_mod: None,
        };

        let selected = config.select_mod(None)?;
        assert_eq!(selected.name, "Alpha");
        Ok(())
    }
}
