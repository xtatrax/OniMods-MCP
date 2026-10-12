use anyhow::{Result, bail};
use clap::{Parser, Subcommand, ValueEnum};
use std::path::PathBuf;

mod archive;
mod build;
mod config;
mod dev;
mod doctor;
mod info;
mod init;
mod install;
mod publish;
mod setup;
mod steam;
mod uninstall;

#[derive(Parser)]
#[command(name = "onim")]
#[command(about = "Oxygen Not Included の Mod 開発用 CLI ツール")]
#[command(version)]
struct Cli {
    #[command(subcommand)]
    command: Commands,

    /// 設定ファイルのパスを指定
    #[arg(short, long, global = true)]
    config: Option<PathBuf>,

    /// 操作対象の Mod を指定（省略時は既定または先頭の Mod を使用）
    #[arg(short, long, global = true)]
    r#mod: Option<String>,
}

#[derive(Subcommand)]
enum Commands {
    /// プロジェクトを初期設定（ゲームのパスを検出し、設定ファイルを作成）
    Setup,
    /// テンプレートから新しい Mod を作成
    Init {
        /// Mod 名（英語、空白なし）
        name: String,
        /// 作者名
        #[arg(short, long)]
        author: Option<String>,
        /// Mod の説明
        #[arg(short, long)]
        desc: Option<String>,
        /// Mod のバージョン
        #[arg(long, default_value = "0.1.7")]
        mod_version: String,
    },
    /// Mod をビルド（既定は Debug、--release を指定すると Release）
    Build {
        #[arg(short, long)]
        release: bool,
        /// 設定済みの全 Mod をビルド
        #[arg(long)]
        all: bool,
    },
    /// 開発モードでビルドし、ゲームの Dev ディレクトリへインストール
    Dev {
        /// 設定済みの全 Mod を開発モードでビルドしてインストール
        #[arg(long)]
        all: bool,
    },
    /// ゲームの Local ディレクトリへ正式にインストール
    Install,
    /// ゲームのディレクトリから Mod をアンインストール
    Uninstall {
        /// アンインストールの対象範囲
        #[arg(short, long, value_enum, default_value_t = UninstallScope::All)]
        scope: UninstallScope,
    },
    /// インストール済みの Mod 情報を表示
    Info,
    /// Steam ワークショップへ公開
    Publish {
        /// 更新内容の入力を省略し、OniUploader GUI を直接使用
        #[arg(long)]
        gui: bool,
        /// 更新内容の入力を省略し、最新の変更履歴をアップロード時の説明に使用
        #[arg(long)]
        auto_note: bool,
        /// すべての入力要求を省略し、GUI を起動せず、単一 ZIP 公開スクリプトの使用を案内
        #[arg(long, visible_alias = "yes", conflicts_with = "gui")]
        non_interactive: bool,
        /// ビルドと Workshop VDF の生成のみ行い、アップロードしない
        #[arg(long, conflicts_with = "gui")]
        dry_run: bool,
    },
    /// 設定済みの全 Mod を一覧表示
    List,
    /// このマシンの ONI Mod 開発環境を確認
    Doctor,
}

#[derive(Clone, ValueEnum)]
pub enum UninstallScope {
    Dev,
    Local,
    All,
}

fn main() -> Result<()> {
    let cli = Cli::parse();

    // Setup、Init、Doctor は厳密な設定読み込みを必要としない。
    match &cli.command {
        Commands::Setup => return setup::run(),
        Commands::Init {
            name,
            author,
            desc,
            mod_version,
        } => {
            return init::run(
                name.clone(),
                author.clone(),
                desc.clone(),
                mod_version.clone(),
                cli.config,
            );
        }
        Commands::Doctor => return doctor::run(cli.config.clone()),
        _ => {}
    }

    let cfg = config::load(cli.config)?;

    match cli.command {
        Commands::Build { release, all } => {
            if all {
                if cli.r#mod.is_some() {
                    bail!("--all と -m/--mod は同時に指定できません");
                }
                for selected in cfg.select_all_mods()? {
                    build::run(&cfg, &selected, release)?;
                }
                Ok(())
            } else {
                let selected = cfg.select_mod(cli.r#mod)?;
                build::run(&cfg, &selected, release)
            }
        }
        Commands::Dev { all } => {
            if all {
                if cli.r#mod.is_some() {
                    bail!("--all と -m/--mod は同時に指定できません");
                }
                for selected in cfg.select_all_mods()? {
                    dev::run(&cfg, &selected)?;
                }
                Ok(())
            } else {
                let selected = cfg.select_mod(cli.r#mod)?;
                dev::run(&cfg, &selected)
            }
        }
        Commands::Install => {
            let selected = cfg.select_mod(cli.r#mod)?;
            install::run(&cfg, &selected)
        }
        Commands::Uninstall { scope } => {
            let selected = cfg.select_mod(cli.r#mod)?;
            uninstall::run(&cfg, &selected, scope)
        }
        Commands::Info => info::run(&cfg),
        Commands::Publish {
            gui,
            auto_note,
            non_interactive,
            dry_run,
        } => {
            let selected = cfg.select_mod(cli.r#mod)?;
            publish::run(
                &cfg,
                &selected,
                publish::PublishOptions {
                    use_gui: gui,
                    auto_note,
                    non_interactive,
                    dry_run,
                },
            )
        }
        Commands::List => {
            println!("設定済みの Mod:");
            let default = cfg.default_mod.clone();
            for (name, m) in &cfg.mods {
                let marker = if Some(name.to_string()) == default {
                    " ← 既定"
                } else {
                    ""
                };
                println!("  • {}  ({}){}", name, m.path.display(), marker);
            }
            Ok(())
        }
        _ => unreachable!(),
    }
}
