use anyhow::{Result, bail};
use std::env;
#[cfg(unix)]
use std::fs;
use std::path::{Path, PathBuf};

use crate::config;

pub fn run(explicit_config_path: Option<PathBuf>) -> Result<()> {
    let diagnostics = config::read_doctor_diagnostics(explicit_config_path)?;
    let mut issues = Vec::new();

    println!("🩺 onim 環境診断\n");
    check_config(
        "onim 設定ファイル",
        &diagnostics.config_path,
        diagnostics.config_error.as_deref(),
        &mut issues,
    );
    check_config(
        "Directory.Build.props",
        &diagnostics.props_path,
        diagnostics.game_path_error.as_deref(),
        &mut issues,
    );

    match (&diagnostics.game_path, &diagnostics.managed_path) {
        (Some(game_path), Some(managed_path)) => {
            check_directory("ONI ゲームのパス", game_path, &mut issues);
            check_directory(
                "マネージドアセンブリのディレクトリ",
                managed_path,
                &mut issues,
            );
            check_file(
                "Assembly-CSharp.dll",
                &managed_path.join("Assembly-CSharp.dll"),
                &mut issues,
            );
        }
        _ => println!(
            "❌ ONI ゲームのパスとマネージドアセンブリのディレクトリ: Directory.Build.props から読み取れません"
        ),
    }

    match (&diagnostics.game_mods_dir, &diagnostics.game_mods_dir_error) {
        (Some(path), None) => check_directory("ゲームの Mod ルートディレクトリ", path, &mut issues),
        (_, Some(error)) => {
            println!("❌ ゲームの Mod ルートディレクトリ: 特定できません（{error}）");
            issues.push(format!(
                "ゲームの Mod ルートディレクトリを特定できません: {error}"
            ));
        }
        _ => unreachable!(),
    }

    println!("\n🔧 外部ツール:");
    for tool in required_tools() {
        if tool_on_path(tool) {
            println!("   ✅ {tool}");
        } else {
            println!("   ❌ {tool}（PATH 内に見つかりません）");
            issues.push(format!("外部ツールが見つかりません: {tool}"));
        }
    }

    println!("\n📦 設定済み Mod のソースコード:");
    match diagnostics.mods {
        Some(mods) if mods.is_empty() => {
            println!("   ❌ Mod が設定されていません");
            issues.push("Mod が設定されていません".to_string());
        }
        Some(mods) => {
            let mut mods: Vec<_> = mods.iter().collect();
            mods.sort_unstable_by_key(|(key, _)| *key);
            for (key, mod_cfg) in mods {
                let source = mod_cfg.project_abs(&diagnostics.repo_root);
                check_directory(
                    &format!("Mod {} ({})", mod_cfg.mod_name(key), key),
                    &source,
                    &mut issues,
                );
            }
        }
        None => println!(
            "   ❌ 設定ファイルを解析できないため、Mod のソースコードのパスを読み取れません"
        ),
    }

    if issues.is_empty() {
        println!("\n✅ 環境診断に合格しました");
        Ok(())
    } else {
        println!("\n❌ 環境診断に失敗しました（{} 件）:", issues.len());
        for issue in &issues {
            println!("   • {issue}");
        }
        bail!("onim doctor が環境の問題を {} 件検出しました", issues.len());
    }
}

fn check_config(label: &str, path: &Path, error: Option<&str>, issues: &mut Vec<String>) {
    match error {
        Some(error) => {
            println!("❌ {label}：{}（{error}）", path.display());
            issues.push(error.to_string());
        }
        None => println!("✅ {label}：{}", path.display()),
    }
}

fn check_directory(label: &str, path: &Path, issues: &mut Vec<String>) {
    if path.is_dir() {
        println!("✅ {label}：{}", path.display());
    } else {
        println!("❌ {label}：{}", path.display());
        issues.push(format!(
            "{label} が存在しないか、ディレクトリではありません: {}",
            path.display()
        ));
    }
}

fn check_file(label: &str, path: &Path, issues: &mut Vec<String>) {
    if path.is_file() {
        println!("✅ {label}：{}", path.display());
    } else {
        println!("❌ {label}：{}", path.display());
        issues.push(format!("{label} が存在しません: {}", path.display()));
    }
}

fn required_tools() -> &'static [&'static str] {
    #[cfg(target_os = "windows")]
    {
        &["dotnet", "tar"]
    }

    #[cfg(not(target_os = "windows"))]
    {
        &["dotnet", "tar", "unzip"]
    }
}

fn tool_on_path(tool: &str) -> bool {
    let Some(paths) = env::var_os("PATH") else {
        return false;
    };
    let file_name = format!("{tool}{}", env::consts::EXE_SUFFIX);
    env::split_paths(&paths).any(|directory| is_executable(&directory.join(&file_name)))
}

fn is_executable(path: &Path) -> bool {
    if !path.is_file() {
        return false;
    }

    #[cfg(unix)]
    {
        use std::os::unix::fs::PermissionsExt;

        fs::metadata(path).is_ok_and(|metadata| metadata.permissions().mode() & 0o111 != 0)
    }

    #[cfg(not(unix))]
    {
        true
    }
}

#[cfg(test)]
mod tests {
    use super::required_tools;

    #[cfg(target_os = "windows")]
    #[test]
    fn required_tools_should_use_windows_archive_support() {
        assert_eq!(required_tools(), ["dotnet", "tar"]);
    }

    #[cfg(not(target_os = "windows"))]
    #[test]
    fn required_tools_should_include_unzip_off_windows() {
        assert_eq!(required_tools(), ["dotnet", "tar", "unzip"]);
    }
}
