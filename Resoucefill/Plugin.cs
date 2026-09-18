using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Timers;
using Terraria;
using Terraria.ID;
using Terraria.DataStructures;
using Terraria.GameContent.Tile_Entities;
using TerrariaApi.Server;
using TShockAPI;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;

namespace Resourcefill
{
    
    [ApiVersion(2, 1)]
    public class Resourcefill  : TerrariaPlugin
    {
        // 插件元信息
        public override Version Version => new Version("1.2.0");
        public override string Name => "Resourcefill";
        public override string Author => "唉唉有改箱子生成";
        public override string Description => "自动刷新世界资源插件";

        // 最大随机生成循环次数
        private const int MaxGenLoopTimeout = 2000;
        private readonly System.Timers.Timer _autoRefillTimer = new System.Timers.Timer();

        public Resourcefill (Main game) : base(game)
        {
        }

        public override void Initialize()
        {
            MannequinConfigManager.LoadConfig();
            ServerApi.Hooks.GamePostInitialize.Register(this, OnInitialize);
            Commands.ChatCommands.Add(new Command("resourcefill.use", ResourceFillCommand, "resourcefill", "rf"));
            //TSPlayer.All.SendInfoMessage("ResourceFill 资源填充插件加载完成");

            // 读取基础配置
            bool cfgOk = Utils.ReadConfig();
            if (!cfgOk)
            {
                TShock.Log.Error("基础配置读取失败！");
            }
            // 加载箱子战利品配置
            Utils.LoadChestLootConfig();
        }
        
        public static bool ReadConfig()
        {
            // 缺失实例化，补上
            Utils.ChestConfig = new ChestConfig();
            // 后续JSON读取逻辑写这里
            return true;
        }
        private void OnInitialize(EventArgs args)
        {
            _autoRefillTimer.Elapsed += OnAutoRefillTick;
            
            // 增加config空校验
            if (Utils.ChestConfig == null)
            {
                TShock.Log.Error("配置未加载，自动补货禁用");
                return;
            }
            if (Utils.ChestConfig.AutomaticallRefill)
            {
                _autoRefillTimer.Interval = Utils.ChestConfig.AutoRefillTimerInMinutes * 60 * 1000;
                _autoRefillTimer.Start();
                if (Utils.ChestLootCfg != null)
                    Utils.DebugLog($"自动补货已开启，间隔 {Utils.ChestConfig.AutoRefillTimerInMinutes} 分钟");
            }
        }

        /// <summary>定时自动补货循环（仅保留箱子、罐子、生命水晶）</summary>
        private void OnAutoRefillTick(object sender, ElapsedEventArgs e)
        {
            // 自动生成箱子
            if (Utils.ChestConfig.ReplenChests)
            {
                Utils.DebugLog("正在自动生成埋藏箱子...");
                PrivateResourcefill(GenType.manacrystal, Utils.ChestConfig.ManaCrystalAmount, out _, 0, null, -1, -1, null);
            }

            // 自动生成生命水晶
            if (Utils.ChestConfig.ReplenLifeCrystals)
            {
                Utils.DebugLog("正在自动生成生命水晶...");
                PrivateResourcefill (GenType.lifecrystals, Utils.ChestConfig.LifeCrystalAmount, out _, 0);
            }
            // 自动生成魔力水晶
            if (Utils.ChestConfig.ReplenManaCrystals)
            {
                Utils.DebugLog("正在自动生成魔力水晶...");
                PrivateResourcefill(GenType.manacrystal, Utils.ChestConfig.ManaCrystalAmount, out _, 0);
            }
            // 自动生成罐子
            if (Utils.ChestConfig.ReplenPots)
            {
                Utils.DebugLog("正在自动生成罐子...");
                PrivateResourcefill (GenType.pots, Utils.ChestConfig.PotsAmount, out _, 0);
            }
        }

        /// <summary>底层资源生成核心逻辑（仅保留箱子、罐子、生命水晶）</summary>
        private bool PrivateResourcefill(GenType type, int targetAmount, out int genCount, ushort oreType = 0, CommandArgs args = null, int forceStyle = -1, int forceTileId = -1, GenRegion limitRegion = null)
        {
            genCount = 0;
            for (int i = 0; i < MaxGenLoopTimeout; i++)
            {
                bool success = false;
                int randX = WorldGen.genRand.Next(1, Main.maxTilesX);

                switch (type)
                {
                   case GenType.chests:
{
    if (targetAmount == 0)
    {
        int totalChest = 0;
        int emptyRemoved = 0;
        for (int j = 0; j < Main.chest.Length; j++)
        {
            if (Main.chest[j] == null) continue;
            totalChest++;

            bool isEmpty = true;
            foreach (Item it in Main.chest[j].item)
            {
                if (it.type != 0)
                {
                    isEmpty = false;
                    break;
                }
            }

            if (isEmpty)
            {
                int oldChestX = Main.chest[j].x;
                int oldChestY = Main.chest[j].y;
                WorldGen.KillTile(oldChestX, oldChestY, false, false, false);
                Main.chest[j] = null;
                emptyRemoved++;
            }
        }
        if (args != null)
        {
            Utils.SendPlayerMsg(args.Player, $"已清理 {emptyRemoved}/{totalChest} 个空箱子");
        }
        genCount = emptyRemoved;
        return true;
    }
    
    int useX = Math.Clamp(randX, 1, Main.maxTilesX - 2);
    int randY = WorldGen.genRand.Next((int)Main.worldSurface - 12, Main.maxTilesY);
    randY = Math.Clamp(randY, 1, Main.maxTilesY - 2);

// 新增：如果传入限定区域，坐标不在范围内则跳过本次随机
    if (limitRegion != null && !Utils.IsPointInRegion(useX, randY, limitRegion))
    {
        continue;
    }
    
    bool canGen = !TShock.Regions.InAreaRegion(useX, randY).Any() || Utils.ChestConfig.GenerateInProtectedAreas;
    if (!canGen) break;

    int groundY = randY;
    while (groundY < Main.maxTilesY - 10 && !Main.tile[useX, groundY].active())
        groundY++;
    groundY = Math.Clamp(groundY, 1, Main.maxTilesY - 2);
    if (groundY >= Main.maxTilesY - 8) break;

    int chestX = useX;
    int chestY = groundY - 1;

    // 强制方块ID优先
    int targetTileId;
    if (forceTileId != -1)
    {
        targetTileId = forceTileId;
    }
    else
    {
        targetTileId = WorldGen.genRand.Next(2) == 0 ? 21 : 467;
    }

    int randomStyle;
    // 根据方块ID限制style区间
    if (targetTileId == 21)
        randomStyle = forceStyle == -1 ? WorldGen.genRand.Next(0, 52) : forceStyle;
    else // 467 样式0~37
        randomStyle = forceStyle == -1 ? WorldGen.genRand.Next(0, 38) : forceStyle;

    // 地面实心校验，同时限制坐标防止越界
    int checkY = Math.Clamp(chestY + 1, 1, Main.maxTilesY - 2);
    ITile groundCheckTile = Main.tile[chestX, checkY];
    if (!groundCheckTile.active())
        break;

    // 输出日志确认当前生成方块ID
    Utils.DebugLog($"生成箱子坐标({chestX},{chestY}) 方块ID:{targetTileId} Style:{randomStyle}");
    int chestIndex = WorldGen.PlaceChest(chestX, chestY, (ushort)targetTileId, false, randomStyle);
    if (chestIndex == -1) break;

    Chest chest = Main.chest[chestIndex];
    Array.Clear(chest.item, 0, chest.item.Length);

    // 填充战利品：传入方块ID+Style双参数
    var tier = Utils.ChestLootCfg.GetTier(targetTileId, randomStyle);
    if (tier != null)
    {
        Utils.FillChestLoot(chest, targetTileId, randomStyle, Utils.ChestLootCfg);
    }
    else
    {
        // 无配置兜底金币
        Item fallbackItem = new Item();
        fallbackItem.SetDefaults(ItemID.GoldCoin);
        fallbackItem.stack = WorldGen.genRand.Next(1, 10);
        chest.item[0] = fallbackItem;
    }

    // 同步全服玩家
    foreach (TSPlayer plr in TShock.Players)
    {
        if (plr != null && plr.Active)
            plr.SendData(PacketTypes.ChestItem, "", chestIndex);
    }

    success = true;
    break;
}
                    case GenType.pots:
                    {
                        int randY = WorldGen.genRand.Next((int)Main.worldSurface - 12, Main.maxTilesY);
                        bool canGen = !TShock.Regions.InAreaRegion(randX, randY).Any() || Utils.ChestConfig.GenerateInProtectedAreas;
                        success = canGen && WorldGen.PlacePot(randX, randY, 28, 0);
                        break;
                    }
                    case GenType.lifecrystals:
                    {
                        int randY = WorldGen.genRand.Next((int)Main.worldSurface - 12, Main.maxTilesY);
                        bool canGen = !TShock.Regions.InAreaRegion(randX, randY).Any() || Utils.ChestConfig.GenerateInProtectedAreas;
                        success = canGen && WorldGen.AddLifeCrystal(randX, randY);
                        break;
                    }
                    case GenType.manacrystal:
                    {
                        int randY = WorldGen.genRand.Next((int)Main.worldSurface - 12, Main.maxTilesY);
                        bool canGen = !TShock.Regions.InAreaRegion(randX, randY).Any() || Utils.ChestConfig.GenerateInProtectedAreas;
                        success = canGen && Utils.AddManaCrystal(randX, randY);
                        break;
                    }
                }

                if (success)
                {
                    genCount++;
                    if (genCount >= targetAmount)
                        return true;
                }
            }
            return false;
        }

        /// <summary>/replen 指令入口</summary>
        private void ResourceFillCommand(CommandArgs args)
        {
            
            List<string> param = args.Parameters;
            var op = args.Player;
            GenRegion targetRegion = null;
            int argOffset = 0;

            // 第一步：检测第一个参数是不是区域名称
            if (param.Count >= 1)
            {
                string firstArg = param[0];
                targetRegion = Utils.GetGenRegionByName(firstArg);
                if (targetRegion != null)
                {
                    // 识别到区域，功能参数从第二位开始读取
                    argOffset = 1;
                }
            }

            // 剩余参数列表（跳过区域名）
            List<string> realParams = param.Skip(argOffset).ToList();
            if (realParams.Count == 0)
            {
                Utils.SendPlayerMsg(args.Player, "参数不足！使用 /rf help 查看指令");
                return;
            }

            // 正确：子指令从裁剪后的参数读取，不是原始param
            string subCmd = realParams[0].ToLower();
            // 重载全部配置（包含模特）
            if (subCmd == "reload")
            {
                Utils.ConfigReload();
                // 重载模特配置
                MannequinConfigManager.LoadConfig();
                _autoRefillTimer.Interval = Utils.ChestConfig.AutoRefillTimerInMinutes * 60 * 1000;
                if (Utils.ChestConfig.AutomaticallRefill)
                    _autoRefillTimer.Start();
                else
                    _autoRefillTimer.Stop();
                Utils.SendPlayerMsg(args.Player, "[Resourcefill] 全局配置、箱子、模特穿戴配置全部重载完成！");
                return;
            }
if (subCmd == "exportmannequin" || subCmd == "em")
{
    // 初始化导出配置，用于汇总全部模特数据
    MannequinGlobalConfig exportCfg = new MannequinGlobalConfig();
    // 用字典统计每个槽位出现的物品ID总权重、前缀总权重
    Dictionary<int, int>[] slotItemWeights = new Dictionary<int, int>[9];
    Dictionary<int, int>[] slotPrefixWeights = new Dictionary<int, int>[9];
    int[] slotEmptyCount = new int[9];
    int totalDollCount = 0;

    // 初始化9个槽位的统计容器
    for (int i = 0; i < 9; i++)
    {
        slotItemWeights[i] = new Dictionary<int, int>();
        slotPrefixWeights[i] = new Dictionary<int, int>();
        slotEmptyCount[i] = 0;
    }

    const ushort DisplayDollTileId = 470;
    List<Point16> handledPos = new List<Point16>();

    // 全地图遍历所有模特
    for (int x = 0; x < Main.maxTilesX; x++)
    {
        for (int y = 0; y < Main.maxTilesY; y++)
        {
            ITile tile = Main.tile[x, y];
            if (!tile.active() || tile.type != DisplayDollTileId)
                continue;

            int frameX = tile.frameX;
            int realX = x - frameX / 36;
            int realY = y;
            Point16 dollTopLeft = new Point16(realX, realY);
            if (handledPos.Contains(dollTopLeft)) continue;
            handledPos.Add(dollTopLeft);

            TEDisplayDoll targetDoll = null;
            bool findSuccess = TileEntity.TryGetAt<TEDisplayDoll>(realX, realY, out targetDoll);
            if (!findSuccess || targetDoll == null) continue;

            totalDollCount++;
            // 反射读取模特装备数组
            Type dollType = targetDoll.GetType();
            FieldInfo equipField = dollType.GetField("_equip", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetField);
            if (equipField == null) continue;

            Item[] equipSlots = (Item[])equipField.GetValue(targetDoll);
            if (equipSlots == null || equipSlots.Length < 9) continue;

            // 循环9个槽位统计数据
            for (int slotIdx = 0; slotIdx < 9; slotIdx++)
            {
                Item slotItem = equipSlots[slotIdx];
                if (slotItem.type == 0)
                {
                    // 该槽为空，空计数+1
                    slotEmptyCount[slotIdx]++;
                    continue;
                }
                // 统计物品权重，每出现一次权重+1
                if (slotItemWeights[slotIdx].ContainsKey(slotItem.type))
                    slotItemWeights[slotIdx][slotItem.type] += 1;
                else
                    slotItemWeights[slotIdx][slotItem.type] = 1;

                // 统计前缀权重，每出现一次权重+1
                if (slotPrefixWeights[slotIdx].ContainsKey(slotItem.prefix))
                    slotPrefixWeights[slotIdx][slotItem.prefix] += 1;
                else
                    slotPrefixWeights[slotIdx][slotItem.prefix] = 1;
            }
        }
    }

    // 无模特直接返回
    if (totalDollCount == 0)
    {
        Utils.SendPlayerMsg(args.Player, "地图内未找到任何模特方块(470)，导出终止");
        return;
    }

    // 将统计数据填充到导出配置实体
    FillAllSlotFromStats(exportCfg.Head, slotItemWeights[0], slotPrefixWeights[0], slotEmptyCount[0], totalDollCount);
    FillAllSlotFromStats(exportCfg.Body, slotItemWeights[1], slotPrefixWeights[1], slotEmptyCount[1], totalDollCount);
    FillAllSlotFromStats(exportCfg.Legs, slotItemWeights[2], slotPrefixWeights[2], slotEmptyCount[2], totalDollCount);
    FillAllSlotFromStats(exportCfg.Acc1, slotItemWeights[3], slotPrefixWeights[3], slotEmptyCount[3], totalDollCount);
    FillAllSlotFromStats(exportCfg.Acc2, slotItemWeights[4], slotPrefixWeights[4], slotEmptyCount[4], totalDollCount);
    FillAllSlotFromStats(exportCfg.Acc3, slotItemWeights[5], slotPrefixWeights[5], slotEmptyCount[5], totalDollCount);
    FillAllSlotFromStats(exportCfg.Acc4, slotItemWeights[6], slotPrefixWeights[6], slotEmptyCount[6], totalDollCount);
    FillAllSlotFromStats(exportCfg.Acc5, slotItemWeights[7], slotPrefixWeights[7], slotEmptyCount[7], totalDollCount);
    FillAllSlotFromStats(exportCfg.Mount, slotItemWeights[8], slotPrefixWeights[8], slotEmptyCount[8], totalDollCount);

    // 覆盖写入配置文件
    string jsonOut = JsonConvert.SerializeObject(exportCfg, Formatting.Indented);
    File.WriteAllText(MannequinConfigManager.ConfigPath, jsonOut);
    Utils.SendPlayerMsg(args.Player, $"扫描完成，共读取 {totalDollCount} 个模特\n已覆盖 Resourcefill/mannequin_config.json\n执行 /rf reload 重载配置生效");
    return;
}
// 填充全图模特指令
            if (subCmd == "fillmannequin" || subCmd == "fm")
{
    if (Utils.ChestLootCfg == null)
    {
        Utils.SendPlayerMsg(args.Player, "战利品配置未加载，请先执行 /rf reload");
        return;
    }

    int filledCount = 0;
    int tileMatchCount = 0;
    int dollFindCount = 0;
    const ushort DisplayDollTileId = 470;
    List<Point16> handledPos = new List<Point16>();

    // 限定遍历边界
    int startX = targetRegion == null ? 0 : targetRegion.XMin;
    int endX = targetRegion == null ? Main.maxTilesX : targetRegion.XMax;
    int startY = targetRegion == null ? 0 : targetRegion.YMin;
    int endY = targetRegion == null ? Main.maxTilesY : targetRegion.YMax;

    for (int x = startX; x < endX; x++)
    {
        for (int y = startY; y < endY; y++)
        {
            ITile tile = Main.tile[x, y];
            if (!tile.active() || tile.type != DisplayDollTileId)
                continue;

            tileMatchCount++;
            int frameX = tile.frameX;
            int realX = x - frameX / 36;
            int realY = y;

            // 二次校验坐标在区域内（防止模特左上角超出区域）
            if (targetRegion != null && !Utils.IsPointInRegion(realX, realY, targetRegion))
                continue;

            Point16 dollTopLeft = new Point16(realX, realY);
            if (handledPos.Contains(dollTopLeft))
                continue;
            handledPos.Add(dollTopLeft);

            TEDisplayDoll targetDoll = null;
            bool findSuccess = TileEntity.TryGetAt<TEDisplayDoll>(realX, realY, out targetDoll);
            if (!findSuccess || targetDoll == null)
                continue;
            dollFindCount++;

            bool fillSuccess = Utils.FillDisplayDollReflect(targetDoll);
            if (fillSuccess)
            {
                filledCount++;
                TSPlayer.All.SendTileRect((short)realX, (short)realY, 2, 3);
            }
        }
    }

    string regionTip = targetRegion == null ? "全地图" : $"区域[{targetRegion.RegionName}] X{startX}~{endX} Y{startY}~{endY}";
    Utils.SendPlayerMsg(args.Player, $"扫描范围：{regionTip}\n匹配方块{tileMatchCount} | 独立模特{handledPos.Count} | 找到实体{dollFindCount} | 成功填充{filledCount}");
    return;
}
            // 帮助页面
            if (subCmd == "help")
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("===== Resourcefill 指令帮助 =====");
                sb.AppendLine("/rf [区域名] [类型] [数量] 在指定区域手动生成资源，不带区域则全图");
                sb.AppendLine("/rf [区域名] fm / /rf [区域名] fillmannequin 仅填充指定矩形区域内模特");
                sb.AppendLine("/rf reload 重载全部配置（基础/箱子战利品/模特穿戴/生成区域）");
                sb.AppendLine("/rf fillmannequin /rf fm 填充全图所有模特");
                sb.AppendLine("支持类型：chests(箱子)、pots(罐子)、lifecrystals(生命水晶)、manacrystal(魔力水晶)");
                sb.AppendLine("/rf testchest [数量] 生成配置内全部样式箱子用于测试");
                sb.AppendLine("/rf fillchest 填充地图内所有原生空箱子");
                sb.AppendLine("/rf exportmannequin /rf em 扫描地图模特导出配置");
                Utils.SendPlayerMsg(args.Player, sb.ToString());
                return;
            }
            if (subCmd == "fillchest")
            {
                if (Utils.ChestLootCfg == null)
                {
                    Utils.SendPlayerMsg(args.Player, "战利品配置未加载，请先执行 /replen reload");
                    return;
                }
                int filled = Utils.FillAllEmptyWorldChests(Utils.ChestLootCfg);
                Utils.SendPlayerMsg(args.Player, $"已自动填充地图内 {filled} 个空箱子，手动输入 /save 保存地图");
                return;
            }
            if (subCmd == "testchest")
            {
                if (Utils.ChestLootCfg == null)
                {
                    Utils.SendPlayerMsg(args.Player, "战利品配置未加载，请先执行 /replen reload");
                    return;
                }
                if (param.Count < 2 || !int.TryParse(param[1], out int spawnNum))
                {
                    Utils.SendPlayerMsg(args.Player, "用法：/rf testchest 20 生成配置内全部方块+样式箱子各20个");
                    return;
                }

                // 读取配置内所有方块+样式组合
                var allChestConfigs = Utils.ChestLootCfg.ChestStyleConfigs;
                if (allChestConfigs.Count == 0)
                {
                    Utils.SendPlayerMsg(args.Player, "配置文件未定义任何箱子！");
                    return;
                }

                int totalSpawned = 0;
                foreach (var cfg in allChestConfigs)
                {
                    int singleGenCount;
                    // 传入强制方块ID、强制style
                    PrivateResourcefill (GenType.chests, spawnNum, out singleGenCount, 0, args, cfg.ChestStyle, cfg.TileId);
                    totalSpawned += singleGenCount;
                }

                Utils.SendPlayerMsg(args.Player, $"已生成配置内全部{allChestConfigs.Count}种箱子，总计成功{totalSpawned}个\n完成后手动输入 /save 保存地图");
                return;
            }
            // 参数长度校验
            if (param.Count < 2)
            {
                Utils.SendPlayerMsg(args.Player, "参数不足！标准格式：/rf [类型] [数量]");
                return;
            }

            // 修复Enum.TryParse 可空报错，拆分变量声明
            GenType genType;
            int targetNum;
            if (!Enum.TryParse(realParams[0], true, out genType) || !int.TryParse(realParams[1], out targetNum))
            {
                Utils.SendPlayerMsg(args.Player, "参数错误！标准格式：/rf [类型] [数量]");
                return;
            }

            bool allGen = PrivateResourcefill(genType, targetNum, out int actualGen, 0, args, -1, -1, targetRegion);
            if (allGen)
                Utils.SendPlayerMsg(args.Player, $"{genType} 全部生成完成！");
            else
                Utils.SendPlayerMsg(args.Player, $"{genType} 未生成满目标数量，仅成功生成 {actualGen} 个");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _autoRefillTimer.Elapsed -= OnAutoRefillTick;
                _autoRefillTimer.Stop();
                _autoRefillTimer.Dispose();
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnInitialize);
            }
            base.Dispose(disposing);
        }
        /// <summary>根据全部模特统计数据生成槽配置，自动计算空概率、物品/前缀权重</summary>
        private void FillAllSlotFromStats(SlotConfig slotCfg, Dictionary<int, int> itemStat, Dictionary<int, int> prefixStat, int emptyNum, int totalDoll)
        {
            slotCfg.Enable = true;
            slotCfg.ItemPool.Clear();
            slotCfg.PrefixPool.Clear();
            // 空槽概率 = 空槽数量 / 总模特数 * 100
            slotCfg.EmptyChance = (int)Math.Round((double)emptyNum / totalDoll * 100);

            // 填充所有出现过的物品，权重为出现次数
            foreach (var kv in itemStat)
            {
                slotCfg.ItemPool.Add(new MannequinItemOption
                {
                    ItemId = kv.Key,
                    Weight = kv.Value
                });
            }
            // 填充所有出现过的前缀，权重为出现次数
            foreach (var kv in prefixStat)
            {
                slotCfg.PrefixPool.Add(new PrefixOption
                {
                    PrefixId = kv.Key,
                    Weight = kv.Value
                });
            }
        }
    }
}