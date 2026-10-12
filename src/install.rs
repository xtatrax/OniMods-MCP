use anyhow::{Context, Result};
use std::env;
use std::fs;
use std::path::PathBuf;

use crate::archive;
use crate::build;
use crate::config::{Config, SelectedMod};

pub fn run(cfg: &Config, selected: &SelectedMod) -> Result<()> {
    let repo_root = match env::var_os("ONI_CLI_REPO_ROOT") {
        Some(path) => PathBuf::from(path),
        None => env::current_dir().context("現在のディレクトリの取得に失敗しました")?,
    };

    // 1. Release でビルドする。
    build::run(cfg, selected, true)?;

    // 2. Local ディレクトリにインストールする。
    let dist = cfg.dist_dir(&repo_root);
    let assembly_name = selected.assembly_name(&repo_root);
    let zip = dist.join(format!("{}.zip", assembly_name));

    if !zip.exists() {
        anyhow::bail!("ビルド成果物が見つかりません: {}", zip.display());
    }

    let local_dir = cfg.local_mod_dir(&selected.name)?;
    println!(
        "\n📥 Local ディレクトリにインストール: {}",
        local_dir.display()
    );

    if local_dir.exists() {
        fs::remove_dir_all(&local_dir).with_context(|| {
            format!(
                "既存の Local ディレクトリの削除に失敗しました: {}",
                local_dir.display()
            )
        })?;
    }
    fs::create_dir_all(&local_dir).with_context(|| {
        format!(
            "Local ディレクトリの作成に失敗しました: {}",
            local_dir.display()
        )
    })?;

    archive::unzip(&zip, &local_dir)?;

    println!("✅ ゲームの Local ディレクトリに正式にインストールしました");
    println!("   ゲームを起動 → Mod 一覧 → '{}' を有効化", selected.name);

    Ok(())
}
