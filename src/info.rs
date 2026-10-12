use anyhow::Result;
use std::collections::HashMap;
use std::fs;
use std::path::PathBuf;

use crate::config::Config;

/// mod.yaml の単純なキーと値の組を解析する。
fn parse_mod_yaml(path: &PathBuf) -> Option<HashMap<String, String>> {
    let content = fs::read_to_string(path).ok()?;
    let mut map = HashMap::new();
    for line in content.lines() {
        let line = line.trim();
        if line.is_empty() || line.starts_with('#') {
            continue;
        }
        if let Some((k, v)) = line.split_once(':') {
            let key = k.trim().to_string();
            let val = v.trim().trim_matches('"').trim_matches('\'').to_string();
            map.insert(key, val);
        }
    }
    Some(map)
}

fn scan_mods_dir(dir: &PathBuf) -> Vec<(String, Option<HashMap<String, String>>)> {
    let mut result = vec![];
    if !dir.exists() {
        return result;
    }
    let Ok(entries) = fs::read_dir(dir) else {
        return result;
    };

    for entry in entries.flatten() {
        let path = entry.path();
        if !path.is_dir() {
            continue;
        }
        let name = path
            .file_name()
            .and_then(|n| n.to_str())
            .unwrap_or("?")
            .to_string();
        let yaml = path.join("mod.yaml");
        let info = if yaml.exists() {
            parse_mod_yaml(&yaml)
        } else {
            None
        };
        result.push((name, info));
    }

    result.sort_by(|a, b| a.0.cmp(&b.0));
    result
}

pub fn run(cfg: &Config) -> Result<()> {
    let mods_dir = cfg.game_mods_dir()?;
    let dev_dir = mods_dir.join("Dev");
    let legacy_dev_dir = mods_dir.join("dev");
    let local_dir = mods_dir.join("Local");
    let steam_dir = mods_dir.join("Steam");

    println!("📁 ゲームの Mod ディレクトリ: {}\n", mods_dir.display());

    // Dev mods
    let dev_mods = scan_mods_dir(&dev_dir);
    if !dev_mods.is_empty() {
        println!("🔧 [Dev] 開発・テスト用 Mod ({} 件):", dev_mods.len());
        for (name, info) in &dev_mods {
            print_mod_info(name, info);
        }
        println!();
    }

    let legacy_dev_mods = scan_mods_dir(&legacy_dev_dir);
    if !legacy_dev_mods.is_empty() {
        println!(
            "⚠️  [dev] 旧形式の小文字ディレクトリ（Linux 版 ONI では検出されません、{} 件）:",
            legacy_dev_mods.len()
        );
        for (name, info) in &legacy_dev_mods {
            print_mod_info(name, info);
        }
        println!();
    }

    // Local mods
    let local_mods = scan_mods_dir(&local_dir);
    if !local_mods.is_empty() {
        println!(
            "📦 [Local] ローカルにインストールした Mod ({} 件):",
            local_mods.len()
        );
        for (name, info) in &local_mods {
            print_mod_info(name, info);
        }
        println!();
    }

    // Steam workshop mods
    let steam_mods = scan_mods_dir(&steam_dir);
    if !steam_mods.is_empty() {
        println!("🌐 [Steam] ワークショップの Mod ({} 件):", steam_mods.len());
        for (name, _info) in &steam_mods {
            println!("   • {} (Steam Workshop)", name);
        }
        println!();
    }

    if dev_mods.is_empty()
        && legacy_dev_mods.is_empty()
        && local_mods.is_empty()
        && steam_mods.is_empty()
    {
        println!("ℹ️  インストール済みの Mod は見つかりませんでした");
    }

    Ok(())
}

fn print_mod_info(name: &str, info: &Option<HashMap<String, String>>) {
    match info {
        Some(map) => {
            let title = map
                .get("title")
                .cloned()
                .unwrap_or_else(|| name.to_string());
            let static_id = map
                .get("staticID")
                .map(|s| format!(" [{}]", s))
                .unwrap_or_default();
            let desc = map.get("description").cloned().unwrap_or_default();
            if desc.is_empty() {
                println!("   • {}{}", title, static_id);
            } else {
                println!("   • {}{} — {}", title, static_id, desc);
            }
        }
        None => {
            println!("   • {} (mod.yaml なし)", name);
        }
    }
}
