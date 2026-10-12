use anyhow::{Context, Result};
use std::env;
use std::path::PathBuf;
use std::process::Command;

use crate::config::{Config, SelectedMod};

pub fn run(cfg: &Config, selected: &SelectedMod, release: bool) -> Result<()> {
    let repo_root = match env::var_os("ONI_CLI_REPO_ROOT") {
        Some(path) => PathBuf::from(path),
        None => env::current_dir().context("現在のディレクトリの取得に失敗しました")?,
    };

    let mod_project = selected.config.project_abs(&repo_root);
    println!(
        "📦 Mod をビルド: {} ({})",
        selected.name,
        mod_project.display()
    );

    let mut cmd = Command::new("dotnet");
    cmd.arg("build").current_dir(&mod_project);

    if release {
        cmd.args(["-c", "Release"]);
        println!("   モード: Release");
    } else {
        println!("   モード: Debug");
    }

    let status = cmd.status().context(
        "dotnet build の実行に失敗しました。.NET SDK がインストールされているか確認してください",
    )?;

    if !status.success() {
        anyhow::bail!("dotnet build に失敗しました");
    }

    // ビルド成果物を表示する。
    let dist = cfg.dist_dir(&repo_root);
    let assembly_name = selected.assembly_name(&repo_root);
    let zip = dist.join(format!("{}.zip", assembly_name));
    let src = dist.join(format!("{}-src.tar.gz", assembly_name));

    println!("✅ ビルドに成功しました！");
    if zip.exists() {
        println!("   📁 {}", zip.display());
    }
    if src.exists() {
        println!("   📁 {}", src.display());
    }

    Ok(())
}
