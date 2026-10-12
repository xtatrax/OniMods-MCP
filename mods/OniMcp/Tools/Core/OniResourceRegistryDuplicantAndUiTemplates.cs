using System.Collections.Generic;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class OniResourceRegistry
    {
        private static void AddDuplicantAndUiResourceTemplates(List<McpResourceTemplateInfo> templates)
        {
            templates.AddRange(new[]
            {
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://farming/planting{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "colony_control",
                                        Title = "栽培スロット",
                                        Description = "領域とキーワードに従って栽培可能な設備、現在の植物、栽培要求を取得する。colony_control domain=bio bioDomain=farming action=list_planting と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://farming/harvestables{?areaId,x1,y1,x2,y2,worldId,query,readyOnly,limit}",
                                        Name = "colony_control",
                                        Title = "収穫可能な対象",
                                        Description = "領域とキーワードに従って植物・作物の収穫状態を取得する。colony_control domain=bio bioDomain=farming action=list_harvestables と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://farming/seeds{?query,limit}",
                                        Name = "colony_control",
                                        Title = "種子カタログ",
                                        Description = "栽培可能な種子の prefab/tag を検索する。colony_control domain=bio bioDomain=farming action=seed_catalog と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://ranching/critters{?areaId,x1,y1,x2,y2,worldId,query,capturableOnly,wrangledOnly,limit}",
                                        Name = "colony_control",
                                        Title = "動物",
                                        Description = "colony_control domain=bio bioDomain=ranching kind=critters action=critters で、領域とキーワードに従って捕獲可能な対象の状態を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://ranching/dropoffs{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "colony_control",
                                        Title = "動物解放地点",
                                        Description = "colony_control domain=bio bioDomain=ranching kind=dropoff action=list で、領域とキーワードに従って動物・魚の解放地点を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://ranching/incubators{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "colony_control",
                                        Title = "孵卵器",
                                        Description = "colony_control domain=bio bioDomain=ranching kind=incubator action=list で、領域とキーワードに従って孵卵器の状態を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://medical/clinics{?areaId,x1,y1,x2,y2,worldId,limit}",
                                        Name = "colony_control",
                                        Title = "医療ベッドと診療所",
                                        Description = "領域に従って医療ベッド・診療所の状態としきい値を取得する。colony_control domain=management kind=medical action=clinics と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://sandbox/story-traits{?query}",
                                        Name = "game_control",
                                        Title = "サンドボックスのストーリー特性",
                                        Description = "サンドボックスの Story Trait Tool で配置できるストーリー特性のテンプレートを取得する。game_control domain=sandbox kind=read action=list_story_traits と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://medical/patients{?worldId,includeHealthy,query,limit}",
                                        Name = "colony_control",
                                        Title = "治療対象の患者",
                                        Description = "複製人間の病気、生命力、医療の割り当て状態を取得する。colony_control domain=management kind=medical action=patients と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://medical/doctor-stations{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "colony_control",
                                        Title = "医務室",
                                        Description = "領域に従って医務室の薬の在庫と治療可能な患者を取得する。colony_control domain=management kind=medical action=doctor_stations と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://dupes/direct-commands{?id,name,limit}",
                                        Name = "dupes_control",
                                        Title = "複製人間への直接指示",
                                        Description = "複製人間への直接操作の入口と対応する MCP ツールを取得する。dupes_control domain=side_screen action=direct_commands と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://dupes/todos{?id,name,query,includeBlocked,includePotentialOnly,limit,taskLimit}",
                                        Name = "dupes_control",
                                        Title = "複製人間の作業予定",
                                        Description = "MinionTodoSideScreen の現在の作業、実行可能な作業、実行を妨げられている作業を取得する。dupes_control domain=side_screen action=todos と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://dupes/equipment{?id,name,slotId,includeAvailable}",
                                        Name = "dupes_control",
                                        Title = "複製人間の装備",
                                        Description = "複製人間の装備スロット、現在の装備、利用可能な装備を取得する。dupes_control domain=side_screen action=equipment と同等。書き込みには dupes_control domain=assignable action=set_slot を使う。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://dupes/priorities{?id,name,choreGroup,includeNonUserPrioritizable}",
                                        Name = "dupes_control",
                                        Title = "複製人間ごとの作業優先度",
                                        Description = "Priorities/Jobs 管理画面での、各 ChoreGroup に対する複製人間ごとの作業優先度を取得する。dupes_control domain=priority action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://dupes/hats{?id,name}",
                                        Name = "dupes_control",
                                        Title = "複製人間の帽子",
                                        Description = "Skills 管理画面の現在の帽子、変更先の帽子、選択可能な帽子の一覧を取得する。dupes_control domain=hat action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://dupes/skills{?id,name,query,limit}",
                                        Name = "dupes_control",
                                        Title = "複製人間のスキル",
                                        Description = "Skills 管理画面のスキルツリー、スキルポイント、習得可能な状態を取得する。dupes_control domain=skill action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://dupes/priority-settings",
                                        Name = "dupes_control",
                                        Title = "複製人間の優先度設定",
                                        Description = "Jobs/Priorities 管理画面の高度なモードの全体設定とリセット動作を取得する。dupes_control domain=priority action=settings_get と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://ui/actions{?kind}",
                                        Name = "game_control",
                                        Title = "許可されている UI Action",
                                        Description = "種類に従って、安全に実行できる UI Action を取得する。game_control domain=ui uiDomain=action action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://sandbox/cell/{x}/{y}",
                                        Name = "game_control",
                                        Title = "サンドボックスのセルのサンプリング",
                                        Description = "指定したセルのサンドボックスブラシのパラメーターを取得する。game_control domain=sandbox kind=read action=sample_cell と同等。",
                                        MimeType = "application/json"
                                    }
            });
        }
    }
}
