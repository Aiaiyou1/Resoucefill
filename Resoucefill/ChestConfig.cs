using System;
using System.Collections.Generic;
using Terraria;

namespace Resourcefill
{
    /// <summary>生成限定矩形区域</summary>
    public class GenRegion
    {
        /// <summary>区域标识名，指令使用：/rf 区域名 fillmannequin</summary>
        public string RegionName { get; set; } = string.Empty;
        /// <summary>左上角X</summary>
        public int XMin { get; set; }
        /// <summary>左上角Y</summary>
        public int YMin { get; set; }
        /// <summary>右下角X</summary>
        public int XMax { get; set; }
        /// <summary>右下角Y</summary>
        public int YMax { get; set; }
    }
    /// <summary>单个物品掉落配置：ID、权重、堆叠区间数组[最小,最大]</summary>
    public class LootItem
    {
        public int ItemId { get; set; }
        public int Weight { get; set; }
        // JSON配置格式 "Stack": [最小堆叠, 最大堆叠]
        public int[] Stack { get; set; } = new int[2];

        // 快捷只读属性，外部代码直接调用不用写数组下标
        public int MinStack => Stack[0];
        public int MaxStack => Stack[1];
        
        // 自定义备注，仅编辑查看，逻辑不使用
        public string Remark { get; set; } = string.Empty;
    }

    public class ChestLootTier
    {
        /// <summary>必出主物品池，必定随机1件</summary>
        public List<LootItem> MainItemPool { get; set; } = new List<LootItem>();
        /// <summary>次要互斥分组集合（核心新逻辑）</summary>
        public List<LootGroup> SecondaryGroups { get; set; } = new List<LootGroup>();
    }

    /// <summary>单个箱子方块+Style完整配置</summary>
    public class ChestStyleConfig
    {
        // 箱子方块ID：21=箱 / 467=更新的宝箱
        public int TileId { get; set; }
        // 该方块下的外观style
        public int ChestStyle { get; set; }
        public string ChestName { get; set; } = string.Empty;
        public ChestLootTier NormalMode { get; set; } = new ChestLootTier();
        public ChestLootTier Hardmode { get; set; } = new ChestLootTier();
        
    }

    /// <summary>全局战利品配置根实体，对应 ChestLootConfig.json</summary>
    public class ChestLootConfig
    {
        public bool EnableHardmodeLoot { get; set; } = true;
        public List<ChestStyleConfig> ChestStyleConfigs { get; set; } = new List<ChestStyleConfig>();

        // 新增：随机前缀总开关
        public bool EnableRandomPrefix { get; set; } = true;
        // 新增：前缀最小ID、最大ID（0~83）
        public int PrefixMinId { get; set; } = 0;
        public int PrefixMaxId { get; set; } = 83;
        /// <summary>根据箱子Style获取对应难度战利品池（无LINQ，纯原生循环）</summary>
        public ChestLootTier GetTier(int tileId, int style)
        {
            foreach (var cfg in ChestStyleConfigs)
            {
                if (cfg.TileId == tileId && cfg.ChestStyle == style)
                {
                    return Main.hardMode && EnableHardmodeLoot ? cfg.Hardmode : cfg.NormalMode;
                }
            }
            return null;
        }
    }
    
    /// <summary>互斥次要物品分组，组内仅随机出1件，组间可共存</summary>
    public class LootGroup
    {
        /// <summary>分组名称（仅配置阅读，无逻辑作用）</summary>
        public string GroupName { get; set; } = string.Empty;
        /// <summary>该组生成概率 0~100（推荐30~50）</summary>
        public int SpawnChance { get; set; }
        /// <summary>组内互斥物品列表，只会随机选一件</summary>
        public List<LootItem> Items { get; set; } = new List<LootItem>();
        
        // 分组备注
        public string Remark { get; set; } = string.Empty;
    }
    
    // ========== 你原有全局配置实体（基础插件配置）==========
    public class ChestConfig
    {
        // 调试日志开关
        public bool DebugLog = false;
        // 自动补货总开关
        public bool AutomaticallRefill = true;
        // 自动补货间隔（分钟）
        public int AutoRefillTimerInMinutes = 10;

        // 各类资源生成开关&数量
        public bool ReplenChests = true;
        public int ChestAmount = 20;

        public bool ReplenLifeCrystals = true;
        public int LifeCrystalAmount = 15;
        
        public bool ReplenManaCrystals = true;
        public int ManaCrystalAmount = 15;
        
        public bool ReplenOres = true;
        public int OreAmount = 30;
        public string[] OreToReplen = { "Iron", "Gold", "Cobalt" };

        public bool ReplenPots = true;
        public int PotsAmount = 50;

        public bool ReplenTrees = true;
        public int TreesAmount = 100;

        // 是否在保护区域内生成资源
        public bool GenerateInProtectedAreas = false;
        
        // ========== 新增：多限定生成区域配置 ==========
        public List<GenRegion> GenRegions { get; set; } = new List<GenRegion>()
        {
            // 默认你要求的大世界完整安全区域 区域1
            new GenRegion()
            {
                RegionName = "区域1",
                XMin = 40,
                YMin = 162,
                XMax = 8360,
                YMax = 2360
            }
        };
    }
}