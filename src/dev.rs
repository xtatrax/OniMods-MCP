use anyhow::{Context, Result};
use serde_json::Value;
use std::env;
use std::fs;
use std::path::{Path, PathBuf};
use std::process::Command;
use std::time::{SystemTime, UNIX_EPOCH};

use crate::archive;
use crate::build;
use crate::config::{Config, SelectedMod};

pub fn run(cfg: &Config, selected: &SelectedMod) -> Result<()> {
    let repo_root = match env::var_os("ONI_CLI_REPO_ROOT") {
        Some(path) => PathBuf::from(path),
        None => env::current_dir().context("現在のディレクトリの取得に失敗しました")?,
    };

    // 1. ビルドする。
    build::run(cfg, selected, false)?;

    // 2. Dev ディレクトリにインストールする。
    let dist = cfg.dist_dir(&repo_root);
    let assembly_name = selected.assembly_name(&repo_root);
    let zip = dist.join(format!("{}.zip", assembly_name));

    if !zip.exists() {
        anyhow::bail!("ビルド成果物が見つかりません: {}", zip.display());
    }

    let dev_dir = cfg.dev_mod_dir(&selected.name)?;
    let legacy_dev_dir = cfg.legacy_dev_mod_dir(&selected.name)?;
    println!("\n🔧 Dev ディレクトリにインストール: {}", dev_dir.display());

    if dev_dir.exists() {
        fs::remove_dir_all(&dev_dir).with_context(|| {
            format!(
                "既存の Dev ディレクトリの削除に失敗しました: {}",
                dev_dir.display()
            )
        })?;
    }
    if legacy_dev_dir != dev_dir && legacy_dev_dir.exists() {
        fs::remove_dir_all(&legacy_dev_dir).with_context(|| {
            format!(
                "旧形式の小文字 dev ディレクトリの削除に失敗しました: {}",
                legacy_dev_dir.display()
            )
        })?;
    }
    fs::create_dir_all(&dev_dir).with_context(|| {
        format!(
            "Dev ディレクトリの作成に失敗しました: {}",
            dev_dir.display()
        )
    })?;

    archive::unzip(&zip, &dev_dir)?;
    enable_dev_mod(cfg, selected, &dev_dir)?;

    println!("✅ ゲームの Dev ディレクトリにインストールしました");

    if is_game_running() {
        println!(
            "⚠️  ゲームが起動中です。新しいバージョンの Mod を読み込むにはゲームを再起動してください"
        );
    } else {
        println!("💡 ゲームは起動していません。起動後に Mod 一覧で有効にしてください");
    }

    Ok(())
}

fn enable_dev_mod(cfg: &Config, selected: &SelectedMod, dev_dir: &Path) -> Result<()> {
    let static_id = read_static_id(dev_dir).unwrap_or_else(|| format!("local.{}", selected.name));
    let mods_file = cfg.game_mods_dir()?.join("mods.json");
    if !mods_file.exists() {
        println!(
            "⚠️  mods.json が見つかりません。初回起動後に '{}' が有効か確認してください",
            selected.name
        );
        return Ok(());
    }

    let content = fs::read_to_string(&mods_file).with_context(|| {
        format!(
            "mods.json の読み取りに失敗しました: {}",
            mods_file.display()
        )
    })?;
    let mut data: Value = serde_json::from_str(&content)
        .with_context(|| format!("mods.json の解析に失敗しました: {}", mods_file.display()))?;
    let (before_enabled, after_enabled) = {
        let mods = data
            .get_mut("mods")
            .and_then(Value::as_array_mut)
            .context("mods.json に mods 配列がありません")?;
        let before_enabled = count_enabled_mods(mods);

        let mut found = false;
        for item in mods.iter_mut() {
            let same_static_id = item
                .get("staticID")
                .and_then(Value::as_str)
                .map(|value| value == static_id)
                .unwrap_or(false);
            let same_dev_id = item
                .get("label")
                .and_then(|label| label.get("id"))
                .and_then(Value::as_str)
                .map(|value| value == selected.name)
                .unwrap_or(false);
            if same_static_id || same_dev_id {
                item["enabled"] = Value::Bool(true);
                item["status"] = Value::Number(1.into());
                ensure_expansion_enabled(item);
                found = true;
            }
        }

        if !found {
            mods.push(new_dev_mod_entry(selected, &static_id));
        }

        (before_enabled, count_enabled_mods(mods))
    };
    data["mod_load_in_progress"] = Value::Bool(false);
    if after_enabled < before_enabled {
        anyhow::bail!(
            "refusing to write mods.json: enabled mod count would drop from {} to {}",
            before_enabled,
            after_enabled
        );
    }
    if after_enabled <= 1 {
        println!(
            "⚠️  mods.json currently has only {} enabled mod(s). If this save depends on other mods, restore them in the ONI Mods menu before loading the save.",
            after_enabled
        );
    }

    let formatted = serde_json::to_string_pretty(&data)?;
    let backup = mods_file.with_extension(format!(
        "json.onim-backup-{}",
        SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .map(|duration| duration.as_secs())
            .unwrap_or(0)
    ));
    fs::write(&backup, &content).with_context(|| {
        format!(
            "mods.json のバックアップに失敗しました: {}",
            backup.display()
        )
    })?;
    fs::write(&mods_file, format!("{}\n", formatted)).with_context(|| {
        format!(
            "mods.json の書き込みに失敗しました: {}",
            mods_file.display()
        )
    })?;
    println!(
        "✅ mods.json で Dev Mod を有効にしました: {}（有効数 {} -> {}、バックアップ: {}）",
        static_id,
        before_enabled,
        after_enabled,
        backup.display()
    );
    Ok(())
}

fn count_enabled_mods(mods: &[Value]) -> usize {
    mods.iter()
        .filter(|item| {
            item.get("enabled").and_then(Value::as_bool) == Some(true)
                || item.get("status").and_then(Value::as_i64) == Some(1)
        })
        .count()
}

fn read_static_id(dev_dir: &Path) -> Option<String> {
    let content = fs::read_to_string(dev_dir.join("mod.yaml")).ok()?;
    content.lines().find_map(|line| {
        let (key, value) = line.split_once(':')?;
        if key.trim() != "staticID" {
            return None;
        }
        let cleaned = value
            .trim()
            .trim_matches('"')
            .trim_matches('\'')
            .to_string();
        if cleaned.is_empty() {
            None
        } else {
            Some(cleaned)
        }
    })
}

fn ensure_expansion_enabled(item: &mut Value) {
    let Some(array) = item.get_mut("enabledForDlc").and_then(Value::as_array_mut) else {
        item["enabledForDlc"] = Value::Array(vec![Value::String("EXPANSION1_ID".to_string())]);
        return;
    };
    if !array
        .iter()
        .any(|value| value.as_str() == Some("EXPANSION1_ID"))
    {
        array.push(Value::String("EXPANSION1_ID".to_string()));
    }
}

fn new_dev_mod_entry(selected: &SelectedMod, static_id: &str) -> Value {
    serde_json::json!({
     "label": {
      "distribution_platform": 4,
      "id": selected.name,
      "title": selected.name,
      "version": 0
     },
     "status": 1,
     "enabled": true,
     "enabledForDlc": ["EXPANSION1_ID"],
     "crash_count": 0,
     "reinstall_path": null,
     "staticID": static_id
    })
}

fn is_game_running() -> bool {
    // Match the game process name only. Never use full-command-line matching
    // (e.g. `pgrep -f`): Steam uploader paths like
    // `.../OxygenNotIncludedUploader/OniUploader64` would false-positive.
    #[cfg(target_os = "linux")]
    {
        process_name_running(&["OxygenNotIncluded", "OxygenNotIncluded.x86_64"])
    }

    #[cfg(target_os = "windows")]
    {
        Command::new("tasklist")
            .arg("/FI")
            .arg("IMAGENAME eq OxygenNotIncluded.exe")
            .output()
            .map(|o| String::from_utf8_lossy(&o.stdout).contains("OxygenNotIncluded.exe"))
            .unwrap_or(false)
    }

    #[cfg(target_os = "macos")]
    {
        process_name_running(&["OxygenNotIncluded"])
    }
}

/// True if any of the exact process names is running (`pgrep -x`).
#[cfg(any(target_os = "linux", target_os = "macos"))]
fn process_name_running(names: &[&str]) -> bool {
    names.iter().any(|name| {
        Command::new("pgrep")
            .args(["-x", name])
            .output()
            .map(|o| o.status.success())
            .unwrap_or(false)
    })
}
