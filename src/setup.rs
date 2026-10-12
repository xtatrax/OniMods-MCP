use anyhow::{Context, Result};
use std::env;
use std::fs;
use std::io::{self, Write};
use std::path::{Path, PathBuf};
use std::process::Command;

const CONFIG_FILE: &str = "onim.toml";
const BUILD_PROPS: &str = "Directory.Build.props";

fn check_command(cmd: &str) -> bool {
    Command::new("sh")
        .args(["-c", &format!("command -v {} > /dev/null 2>&1", cmd)])
        .status()
        .map(|s| s.success())
        .unwrap_or(false)
}

fn prompt(question: &str, default: Option<&str>) -> Result<String> {
    print!("{}", question);
    io::stdout().flush()?;
    let mut buf = String::new();
    io::stdin().read_line(&mut buf)?;
    let trimmed = buf.trim().to_string();
    if trimmed.is_empty() {
        if let Some(d) = default {
            Ok(d.to_string())
        } else {
            Ok(trimmed)
        }
    } else {
        Ok(trimmed)
    }
}

fn managed_relative_path(os: &str) -> &'static str {
    match os {
        "macos" => "OxygenNotIncluded.app/Contents/Resources/Data/Managed",
        _ => "OxygenNotIncluded_Data/Managed",
    }
}

fn managed_path(game_path: &Path, os: &str) -> PathBuf {
    game_path.join(managed_relative_path(os))
}

fn auto_detect() -> Option<PathBuf> {
    for steam_root in crate::steam::library_roots() {
        let game_path = steam_root.join("steamapps/common/OxygenNotIncluded");
        let assembly = managed_path(&game_path, env::consts::OS).join("Assembly-CSharp.dll");

        if assembly.is_file() {
            return Some(game_path);
        }
    }

    None
}

fn validate_game_path(path: &Path) -> Result<Vec<String>> {
    let mut errors = vec![];
    let managed = managed_path(path, env::consts::OS);

    let required = [
        ("Assembly-CSharp.dll", "ゲームの主要ロジック DLL"),
        ("0Harmony.dll", "Harmony パッチフレームワーク"),
        ("UnityEngine.CoreModule.dll", "Unity エンジンのコア"),
    ];

    for (file, desc) in &required {
        let p = managed.join(file);
        if !p.exists() {
            errors.push(format!(
                "{} ({}) が見つかりません: {}",
                file,
                desc,
                p.display()
            ));
        }
    }

    Ok(errors)
}

pub fn run() -> Result<()> {
    println!("🛠️  onim プロジェクトの初期設定\n");

    // 依存ツールを確認する。
    println!("📋 依存ツールを確認中...");
    let mut missing = vec![];

    if !check_command("dotnet") {
        missing.push("dotnet (.NET SDK)");
    }
    #[cfg(not(target_os = "windows"))]
    {
        if !check_command("unzip") {
            missing.push("unzip");
        }
    }
    if !check_command("tar") {
        missing.push("tar");
    }

    if !missing.is_empty() {
        println!("⚠️  必要なツールが不足しています:");
        for m in &missing {
            println!("   ❌ {}", m);
        }
        println!();
        println!("インストールしてから再試行してください:");
        println!("  .NET SDK: https://dotnet.microsoft.com/download");
        #[cfg(not(target_os = "windows"))]
        {
            println!(
                "  unzip: sudo apt install unzip  (またはお使いのパッケージマネージャーでインストール)"
            );
        }
        anyhow::bail!("依存ツールが不足しています");
    }
    println!("✅ 依存ツールはすべてインストール済みです\n");

    let cwd = env::current_dir()?;
    let config_path = cwd.join(CONFIG_FILE);
    let props_path = cwd.join(BUILD_PROPS);

    // 1. ゲームのパスを確認し、必要なら入力を求める。
    let mut game_path = None;

    if config_path.exists() {
        let content = fs::read_to_string(&config_path).unwrap_or_default();
        for line in content.lines() {
            if line.trim_start().starts_with("game_path")
                && let Some((_, val)) = line.split_once('=')
            {
                let p = val.trim().trim_matches('"').trim_matches('\'');
                let pb = PathBuf::from(p);
                if pb.exists() {
                    game_path = Some(pb);
                }
            }
        }
    }

    if let Some(ref path) = game_path {
        println!("既存のゲームパスを検出しました: {}", path.display());
        let answer = prompt("このパスで正しいですか？ [Y/n] ", Some("Y"))?;
        if answer.eq_ignore_ascii_case("n") {
            game_path = None;
        }
    }

    if game_path.is_none()
        && let Some(detected) = auto_detect()
    {
        println!("\nゲームのパスを自動検出しました: {}", detected.display());
        let answer = prompt("このパスを使用しますか？ [Y/n] ", Some("Y"))?;
        if answer.eq_ignore_ascii_case("n") {
            game_path = None;
        } else {
            game_path = Some(detected);
        }
    }

    if game_path.is_none() {
        println!(
            "\nゲームのインストール先ディレクトリを入力してください（OxygenNotIncluded 実行ファイルがある階層）:"
        );
        let input = prompt("> ", None)?;
        if input.is_empty() {
            anyhow::bail!("ゲームのパスが指定されていないため、初期設定を中止しました");
        }
        game_path = Some(PathBuf::from(input));
    }

    let game_path = game_path.ok_or_else(|| anyhow::anyhow!("ゲームのパスが指定されていません"))?;

    // 2. 検証する。
    println!("\n🔍 ゲームファイルを検証中...");
    let errors = validate_game_path(&game_path)?;
    if !errors.is_empty() {
        println!("⚠️  問題が見つかりました:");
        for e in &errors {
            println!("   - {}", e);
        }
        let answer = prompt(
            "ファイルの検証に失敗しました。それでも続行しますか？ [y/N] ",
            Some("N"),
        )?;
        if !answer.eq_ignore_ascii_case("y") {
            anyhow::bail!("初期設定を中止しました");
        }
    } else {
        println!("✅ すべての重要ファイルの検証に成功しました");
    }

    // 3. onim.toml を作成する（Mod 一覧のみ。ゲームのパスは Directory.Build.props に記載）。
    let toml_content = r#"# onim 設定ファイル
# ゲームのパスは Directory.Build.props で一元管理する

# 既定の Mod（-m を指定しない場合に使用）
default_mod = "OniModTemplate"

[mods.OniModTemplate]
path = "mods/OniModTemplate"
"#;

    println!("\n📝 {} を書き込み中...", CONFIG_FILE);
    fs::write(&config_path, toml_content)
        .with_context(|| format!("{} の書き込みに失敗しました", CONFIG_FILE))?;

    // 4. Directory.Build.props を作成する。
    let sep = std::path::MAIN_SEPARATOR_STR;
    let managed_relative = managed_relative_path(env::consts::OS).replace('/', sep);
    let props_content = format!(
        r#"<Project>

  <!-- onim が自動生成した全体設定 -->

  <PropertyGroup>
    <OniGamePath>{}</OniGamePath>
  </PropertyGroup>

  <PropertyGroup>
    <OniManagedPath>$(OniGamePath){}{}</OniManagedPath>
  </PropertyGroup>

  <Target Name="ValidateOniGamePath" BeforeTargets="BeforeBuild">
    <Error Text="ゲームのパスが設定されていません！" Condition="'$(OniGamePath)' == ''" />
  </Target>

</Project>
"#,
        game_path.to_string_lossy().replace('"', "&quot;"),
        sep,
        managed_relative
    );

    println!("📝 {} を書き込み中...", BUILD_PROPS);
    fs::write(&props_path, props_content)
        .with_context(|| format!("{} の書き込みに失敗しました", BUILD_PROPS))?;

    println!("\n✅ 初期設定が完了しました！");
    println!("   ゲームのパス: {}", game_path.display());
    println!("   設定ファイル: {}", config_path.display());
    println!("   MSBuild 設定: {}", props_path.display());
    println!();
    println!("次の手順:");
    println!("  onim build       既定の Mod をビルド");
    println!("  onim init <name> 新しい Mod を作成");

    Ok(())
}

#[cfg(test)]
mod tests {
    use super::managed_relative_path;

    #[test]
    fn uses_current_macos_managed_layout() {
        assert_eq!(
            managed_relative_path("macos"),
            "OxygenNotIncluded.app/Contents/Resources/Data/Managed"
        );
    }

    #[test]
    fn preserves_windows_and_linux_managed_layout() {
        assert_eq!(
            managed_relative_path("windows"),
            "OxygenNotIncluded_Data/Managed"
        );
        assert_eq!(
            managed_relative_path("linux"),
            "OxygenNotIncluded_Data/Managed"
        );
    }
}
