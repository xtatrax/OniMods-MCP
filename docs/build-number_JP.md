# OniMcp のビルド番号

通常の `dotnet build` ごとに、バージョン表示が `0.2.5+build.1` のように増加します。MCP の初期化応答、起動ログ、DLL の InformationalVersion、配布用 mod_info.yaml に同じ番号を使用します。ModVersion と AssemblyVersion は維持します。

カウンターは `mods/OniMcp/.build-number` に保存し、Git の対象外です。Debug / Release で共有し、clean 後も保持します。新しいチェックアウトでは 1 から開始します。番号はビルド開始時に確保するため、失敗したビルドも消費します。復元のみ・設計時ビルドでは増やしません。並列プロセスはロックと原子的なファイル置換で番号の重複を防ぎます。ただし同じ出力ディレクトリへの並列ビルドは避けてください。

CI や再現ビルドでは `-p:OniMcpBuildNumber=123` を指定できます。この場合、ローカルカウンターを変更しません。全チェックアウトを通じた一意性が必要な場合は CI 側の連番を指定してください。リリースタグは引き続き ModVersion を使います。
