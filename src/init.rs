use anyhow::{Context, Result};
use std::env;
use std::fs;
use std::path::PathBuf;

const TEMPLATE_DIR: &str = "mods/OniModTemplate";
const CONFIG_FILE: &str = "onim.toml";

fn pascal_case(input: &str) -> String {
    let mut result = String::new();
    let mut capitalize = true;
    for c in input.chars() {
        if c.is_alphanumeric() {
            if capitalize {
                result.push(c.to_ascii_uppercase());
                capitalize = false;
            } else {
                result.push(c);
            }
        } else {
            capitalize = true;
        }
    }
    result
}

fn replace_in_file(path: &PathBuf, replacements: &[(String, String)]) -> Result<()> {
    let content = fs::read_to_string(path)
        .with_context(|| format!("ファイルの読み取りに失敗しました: {}", path.display()))?;
    let mut new_content = content.clone();
    for (old, new) in replacements {
        new_content = new_content.replace(old, new);
    }
    if new_content != content {
        fs::write(path, new_content)
            .with_context(|| format!("ファイルの書き込みに失敗しました: {}", path.display()))?;
    }
    Ok(())
}

pub fn run(
    name: String,
    author: Option<String>,
    desc: Option<String>,
    mod_version: String,
    _config_path: Option<PathBuf>,
) -> Result<()> {
    let cwd = env::current_dir()?;
    let template = cwd.join(TEMPLATE_DIR);

    if !template.exists() {
        anyhow::bail!(
            "テンプレートのディレクトリが存在しません: {}\n{} ディレクトリが存在するか確認してください",
            template.display(),
            TEMPLATE_DIR
        );
    }

    let mod_dir = cwd.join("mods").join(&name);
    if mod_dir.exists() {
        anyhow::bail!("Mod のディレクトリは既に存在します: {}", mod_dir.display());
    }

    let namespace = pascal_case(&name);
    let author = author.unwrap_or_else(|| "YourName".to_string());
    let desc = desc.unwrap_or_else(|| format!("{} Mod", name));
    let static_id = format!("{}.{}", author, namespace);

    println!("🆕 新しい Mod を作成: {}", name);
    println!("   名前空間: {}", namespace);
    println!("   staticID: {}", static_id);
    println!("   バージョン: {}", mod_version);

    // 1. テンプレートのディレクトリをコピーする。
    copy_dir_all(&template, &mod_dir)?;

    // 2. .csproj ファイルの名前を変更する。
    let old_csproj = mod_dir.join("OniModTemplate.csproj");
    let new_csproj = mod_dir.join(format!("{}.csproj", namespace));
    if old_csproj.exists() {
        fs::rename(&old_csproj, &new_csproj)
            .with_context(|| ".csproj の名前変更に失敗しました".to_string())?;
    }

    // 3. .csproj を編集する（特定の文字列全体を先に置換し、その後で共通の名前を置換する）。
    let replacements_csproj = vec![
        (
            "<ModTitle>OniModTemplate</ModTitle>".to_string(),
            format!("<ModTitle>{}</ModTitle>", name),
        ),
        (
            "<ModDescription>サンプル Mod です</ModDescription>".to_string(),
            format!("<ModDescription>{}</ModDescription>", desc),
        ),
        (
            "<ModStaticID>YourName.OniModTemplate</ModStaticID>".to_string(),
            format!("<ModStaticID>{}</ModStaticID>", static_id),
        ),
        (
            "<ModVersion>0.1.7</ModVersion>".to_string(),
            format!("<ModVersion>{}</ModVersion>", mod_version),
        ),
        (
            "<!-- 注意：supportedContent 在 U55 (2025-03) 已弃用".to_string(),
            "<!-- 注意：supportedContent 在 U55 (2025-03) 已弃用".to_string(),
        ),
        ("OniModTemplate".to_string(), namespace.clone()),
    ];
    replace_in_file(&new_csproj, &replacements_csproj)?;

    // 4. ModInfo.cs を編集する。
    let mod_info = mod_dir.join("ModInfo.cs");
    if mod_info.exists() {
        replace_in_file(
            &mod_info,
            &[("OniModTemplate".to_string(), namespace.clone())],
        )?;
    }

    // 5. Patches/ExamplePatch.cs を編集する。
    let example_patch = mod_dir.join("Patches/ExamplePatch.cs");
    if example_patch.exists() {
        replace_in_file(
            &example_patch,
            &[("OniModTemplate".to_string(), namespace.clone())],
        )?;
    }

    // 6. README.md を編集する。
    let readme = mod_dir.join("README.md");
    if readme.exists() {
        replace_in_file(
            &readme,
            &[
                ("# OniModTemplate".to_string(), format!("# {}", name)),
                ("OniModTemplate/".to_string(), format!("{}/", namespace)),
                (
                    "OniModTemplate.csproj".to_string(),
                    format!("{}.csproj", namespace),
                ),
            ],
        )?;
    }

    // 6. onim.toml を更新する。
    let config_path = cwd.join(CONFIG_FILE);
    if config_path.exists() {
        let content = fs::read_to_string(&config_path)?;
        let entry = format!("\n[mods.{}]\npath = \"mods/{}\"\n", name, name);
        let new_content = format!("{}{}", content.trim_end(), entry);
        fs::write(&config_path, new_content)
            .with_context(|| format!("{} の更新に失敗しました", CONFIG_FILE))?;
        println!("   {} に追加しました", CONFIG_FILE);
    }

    println!("\n✅ Mod '{}' を作成しました！", name);
    println!("   ディレクトリ: {}", mod_dir.display());
    println!();
    println!("次の手順:");
    println!("  onim build -m {}      ビルド", name);
    println!("  onim dev -m {}        開発・テスト", name);

    Ok(())
}

fn copy_dir_all(src: &PathBuf, dst: &PathBuf) -> Result<()> {
    fs::create_dir_all(dst)?;
    for entry in fs::read_dir(src)? {
        let entry = entry?;
        let path = entry.path();
        let file_name = entry.file_name();
        let dest = dst.join(&file_name);

        if path.is_dir() {
            copy_dir_all(&path, &dest)?;
        } else {
            fs::copy(&path, &dest).with_context(|| {
                format!(
                    "ファイルのコピーに失敗しました: {} -> {}",
                    path.display(),
                    dest.display()
                )
            })?;
        }
    }
    Ok(())
}
