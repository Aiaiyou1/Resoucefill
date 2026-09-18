using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Terraria;
using TShockAPI;

namespace Resourcefill
{
    /// <summary>单物品配置：ID+权重</summary>
    public class MannequinItemOption
    {
        public int ItemId { get; set; }
        public int Weight { get; set; }
        
        // 自定义备注，仅编辑查看用，程序逻辑不读取
        public string Remark { get; set; } = string.Empty;
    }

    /// <summary>前缀配置：前缀ID+权重</summary>
    public class PrefixOption
    {
        public int PrefixId { get; set; }
        public int Weight { get; set; }
        public string Remark { get; set; } = string.Empty;
    }

    /// <summary>单个槽位完整配置</summary>
    public class SlotConfig
    {
        public bool Enable { get; set; } = true;
        public List<MannequinItemOption> ItemPool { get; set; } = new();
        public List<PrefixOption> PrefixPool { get; set; } = new();
        public int EmptyChance { get; set; } = 0;
    }

    /// <summary>模特总配置，9个装备槽</summary>
    public class MannequinGlobalConfig
    {
        public SlotConfig Head { get; set; } = new();
        public SlotConfig Body { get; set; } = new();
        public SlotConfig Legs { get; set; } = new();
        public SlotConfig Acc1 { get; set; } = new();
        public SlotConfig Acc2 { get; set; } = new();
        public SlotConfig Acc3 { get; set; } = new();
        public SlotConfig Acc4 { get; set; } = new();
        public SlotConfig Acc5 { get; set; } = new();
        public SlotConfig Mount { get; set; } = new();
    }

    public static class MannequinConfigManager
    {
        // 统一配置文件夹
        public static string ConfigFolder => Path.Combine(TShock.SavePath, "Resourcefill");
        public static string ConfigPath => Path.Combine(ConfigFolder, "mannequin_config.json");
        public static MannequinGlobalConfig GlobalConfig { get; private set; } = new();

        /// <summary>加载/重载配置，仅控制台输出，无玩家广播避免启动崩溃</summary>
        public static void LoadConfig()
        {
            if (!Directory.Exists(ConfigFolder))
                Directory.CreateDirectory(ConfigFolder);

            if (!File.Exists(ConfigPath))
            {
                var defaultCfg = GetDefaultTemplate();
                string json = JsonConvert.SerializeObject(defaultCfg, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
                TShock.Log.ConsoleInfo("[Resourcefill] 生成模特默认配置 mannequin_config.json");
            }
            string content = File.ReadAllText(ConfigPath);
            GlobalConfig = JsonConvert.DeserializeObject<MannequinGlobalConfig>(content);
        }

        /// <summary>默认模板：你指定的全套装备ID</summary>
        private static MannequinGlobalConfig GetDefaultTemplate()
        {
            return new MannequinGlobalConfig
            {
                Head = new SlotConfig { EmptyChance = 10, ItemPool = new(){new(){ItemId=2189,Weight=100}}, PrefixPool = new(){new(){PrefixId=0,Weight=100}} },
                Body = new SlotConfig { EmptyChance = 10, ItemPool = new(){new(){ItemId=3872,Weight=100}}, PrefixPool = new(){new(){PrefixId=0,Weight=100}} },
                Legs = new SlotConfig { EmptyChance = 10, ItemPool = new(){new(){ItemId=3873,Weight=100}}, PrefixPool = new(){new(){PrefixId=0,Weight=100}} },
                Acc1 = new SlotConfig { EmptyChance = 10, ItemPool = new(){new(){ItemId=4954,Weight=100}}, PrefixPool = new(){new(){PrefixId=0,Weight=100}} },
                Acc2 = new SlotConfig { EmptyChance = 10, ItemPool = new(){new(){ItemId=3110,Weight=100}}, PrefixPool = new(){new(){PrefixId=0,Weight=100}} },
                Acc3 = new SlotConfig { EmptyChance = 10, ItemPool = new(){new(){ItemId=4989,Weight=100}}, PrefixPool = new(){new(){PrefixId=0,Weight=100}} },
                Acc4 = new SlotConfig { EmptyChance = 10, ItemPool = new(){new(){ItemId=3810,Weight=100}}, PrefixPool = new(){new(){PrefixId=0,Weight=100}} },
                Acc5 = new SlotConfig { EmptyChance = 10, ItemPool = new(){new(){ItemId=3997,Weight=100}}, PrefixPool = new(){new(){PrefixId=0,Weight=100}} },
                Mount = new SlotConfig { EmptyChance = 10, ItemPool = new(){new(){ItemId=3223,Weight=100}}, PrefixPool = new(){new(){PrefixId=0,Weight=100}} }
            };
        }

        /// <summary>按权重随机物品ID，-1=空槽</summary>
        public static int GetRandomItemId(SlotConfig slotCfg)
        {
            if (slotCfg == null || !slotCfg.Enable) return -1;
            if (Main.rand.Next(100) < slotCfg.EmptyChance) return -1;

            int totalWeight = 0;
            foreach (var opt in slotCfg.ItemPool) totalWeight += opt.Weight;
            if (totalWeight <= 0) return -1;

            int roll = Main.rand.Next(totalWeight);
            int cur = 0;
            foreach (var opt in slotCfg.ItemPool)
            {
                cur += opt.Weight;
                if (roll < cur) return opt.ItemId;
            }
            return slotCfg.ItemPool[0].ItemId;
        }

        /// <summary>随机前缀ID</summary>
        public static int GetRandomPrefix(SlotConfig slotCfg)
        {
            if (slotCfg == null || slotCfg.PrefixPool.Count == 0) return 0;
            int totalWeight = 0;
            foreach (var p in slotCfg.PrefixPool) totalWeight += p.Weight;
            if (totalWeight <= 0) return 0;

            int roll = Main.rand.Next(totalWeight);
            int cur = 0;
            foreach (var p in slotCfg.PrefixPool)
            {
                cur += p.Weight;
                if (roll < cur) return p.PrefixId;
            }
            return slotCfg.PrefixPool[0].PrefixId;
        }

        /// <summary>槽位索引匹配配置</summary>
        public static SlotConfig GetSlotConfigByIndex(int slotIndex)
        {
            return slotIndex switch
            {
                0 => GlobalConfig.Head,
                1 => GlobalConfig.Body,
                2 => GlobalConfig.Legs,
                3 => GlobalConfig.Acc1,
                4 => GlobalConfig.Acc2,
                5 => GlobalConfig.Acc3,
                6 => GlobalConfig.Acc4,
                7 => GlobalConfig.Acc5,
                8 => GlobalConfig.Mount,
                _ => null
            };
        }
    }
}