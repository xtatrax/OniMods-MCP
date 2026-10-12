use anyhow::{Context, Result};
use std::fs;

use crate::UninstallScope;
use crate::config::{Config, SelectedMod};

pub fn run(cfg: &Config, selected: &SelectedMod, scope: UninstallScope) -> Result<()> {
    let dev_dir = cfg.dev_mod_dir(&selected.name)?;
    let legacy_dev_dir = cfg.legacy_dev_mod_dir(&selected.name)?;
    let local_dir = cfg.local_mod_dir(&selected.name)?;

    let mut uninstalled = vec![];

    match scope {
        UninstallScope::Dev | UninstallScope::All => {
            if dev_dir.exists() {
                fs::remove_dir_all(&dev_dir).with_context(|| {
                    format!(
                        "Dev ディレクトリのアンインストールに失敗しました: {}",
                        dev_dir.display()
                    )
                })?;
                uninstalled.push(format!("Dev/{} を削除しました", selected.name));
            }
            if legacy_dev_dir != dev_dir && legacy_dev_dir.exists() {
                fs::remove_dir_all(&legacy_dev_dir).with_context(|| {
                    format!(
                        "旧形式の小文字 dev ディレクトリのアンインストールに失敗しました: {}",
                        legacy_dev_dir.display()
                    )
                })?;
                uninstalled.push(format!(
                    "dev/{} を削除しました（旧形式の誤ったディレクトリ）",
                    selected.name
                ));
            }
        }
        _ => {}
    }

    match scope {
        UninstallScope::Local | UninstallScope::All if local_dir.exists() => {
            fs::remove_dir_all(&local_dir).with_context(|| {
                format!(
                    "Local ディレクトリのアンインストールに失敗しました: {}",
                    local_dir.display()
                )
            })?;
            uninstalled.push(format!("Local/{} を削除しました", selected.name));
        }
        _ => {}
    }

    if uninstalled.is_empty() {
        println!(
            "ℹ️  '{}' はどのディレクトリにもインストールされていません",
            selected.name
        );
    } else {
        println!("✅ '{}' をアンインストールしました:", selected.name);
        for msg in uninstalled {
            println!("   {}", msg);
        }
    }

    Ok(())
}
