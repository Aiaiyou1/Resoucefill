using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID; // 新增，解决ItemID红标
using TShockAPI;
using Newtonsoft.Json;
using System.Reflection;
using Terraria.GameContent.Tile_Entities;
using static Resourcefill.MannequinConfigManager;

// 新增下面两行
using Newtonsoft.Json;

namespace Resourcefill
{
    internal static class Utils
    {
        #region 颜色常量
        /// <summary>插件默认奶黄色文本</summary>
        public static Color DefaultTextColor => new Color(240, 250, 150);
        /// <summary>控制台随机彩色日志</summary>
        public static Color RandomLogColor
        {
            get
            {
                byte r = (byte)Main.rand.Next(180, 250);
                byte g = (byte)Main.rand.Next(180, 250);
                byte b = (byte)Main.rand.Next(180, 250);
                return new Color(r, g, b);
            }
        }
        #endregion

        #region 调试日志
        public static void DebugLog(string text)
        {
            // 先判断config是否实例化，防止空引用
            if (ChestConfig == null || !ChestConfig.DebugLog) 
                return;
            string gradText = TextGradient(text);
            TSPlayer.All.SendMessage(gradText, DefaultTextColor);
            TShock.Log.ConsoleInfo(text, RandomLogColor);
        }
        #endregion

        #region 玩家消息发送
        /// <summary>给玩家发送渐变提示，后台控制台无渐变</summary>
        public static void SendPlayerMsg(TSPlayer player, string content)
        {
            if (player.RealPlayer)
                player.SendMessage(TextGradient(content), DefaultTextColor);
            else
                player.SendMessage(content, DefaultTextColor);
        }
        #endregion

        #region 文本渐变处理
        // 正则缓存：匹配颜色标签、物品图标标签
        private static readonly Regex _tagRegex = new Regex(@"(\[c/([0-9a-fA-F]+):([^\]]+)\]|\[i(?:/s\d+)?:\d+\])");

        /// <summary>自动识别已有颜色/物品标签，剩余文字做渐变</summary>
        public static string TextGradient(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            // 不含标签直接渐变
            if (!text.Contains("[c/") && !text.Contains("[i:"))
                return GradientSingleText(text);

            // 混合标签+普通文本分段处理
            StringBuilder sb = new StringBuilder();
            MatchCollection matches = _tagRegex.Matches(text);
            int lastIndex = 0;

            foreach (Match match in matches)
            {
                // 标签前普通文字渐变
                if (match.Index > lastIndex)
                    sb.Append(GradientSingleText(text.Substring(lastIndex, match.Index - lastIndex)));
                // 原标签原样保留
                sb.Append(match.Value);
                lastIndex = match.Index + match.Length;
            }

            // 末尾剩余文字渐变
            if (lastIndex < text.Length)
                sb.Append(GradientSingleText(text.Substring(lastIndex)));

            return sb.ToString();
        }

        /// <summary>纯无标签文本逐字符渐变上色</summary>
        private static string GradientSingleText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            StringBuilder sb = new StringBuilder();
            Color startColor = new Color(165, 210, 235);
            Color endColor = new Color(245, 250, 175);

            // 修复：移除LINQ Count，原生循环统计有效字符
            int validCharCount = 0;
            foreach (char c in text)
            {
                if (c != '\n' && c != '\r')
                    validCharCount++;
            }
            if (validCharCount == 0) return text;

            int charIndex = 0;
            foreach (char c in text)
            {
                // 换行直接保留不渐变
                if (c == '\n' || c == '\r')
                {
                    sb.Append(c);
                    continue;
                }

                float lerpRate = (float)charIndex / (validCharCount - 1);
                Color currentColor = Color.Lerp(startColor, endColor, lerpRate);
                sb.Append($"[c/{currentColor.Hex3()}:{c}]");
                charIndex++;
            }

            return sb.ToString();
        }
        #endregion

        #region 物品图标快捷方法
        /// <summary>物品图标 [i:物品ID]</summary>
        public static string ItemIcon(int itemId)
            => $"[i:{itemId}]";

        /// <summary>带堆叠数量物品图标 [i/s数量:物品ID]</summary>
        public static string ItemIcon(int itemId, int stack)
            => $"[i/s{stack}:{itemId}]";
        #endregion
        
        #region 配置读写/重载
        public static ChestConfig ChestConfig;
        public static ChestLootConfig ChestLootCfg;

// 必须 public static
        public static string MainConfigPath => Path.Combine(MannequinConfigManager.ConfigFolder, "main_config.json");
        public static bool ReadConfig()
        {
            // 确保Resourcefill文件夹存在
            if (!Directory.Exists(MannequinConfigManager.ConfigFolder))
            {
                Directory.CreateDirectory(MannequinConfigManager.ConfigFolder);
            }
            // 文件不存在生成默认模板
            if (!File.Exists(MainConfigPath))
            {
                ChestConfig defaultCfg = new ChestConfig();
                string json = JsonConvert.SerializeObject(defaultCfg, Formatting.Indented);
                File.WriteAllText(MainConfigPath, json);
                TShock.Log.ConsoleInfo("[Resourcefill] 已生成 Resourcefill/main_config.json 基础配置");
            }
            try
            {
                string jsonText = File.ReadAllText(MainConfigPath);
                ChestConfig = JsonConvert.DeserializeObject<ChestConfig>(jsonText);
                return true;
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"基础配置读取失败：{ex.Message}");
                ChestConfig = new ChestConfig();
                return false;
            }
        }

        public static void ConfigReload()
        {
            ReadConfig();
            LoadChestLootConfig();
            DebugLog("配置文件已重载");
        }
        #endregion

        #region 箱子战利品工具方法（全部移进类内部）
        
        /// <summary>根据方块ID+箱子Style填充自定义战利品（分组互斥逻辑，支持配置化前缀）</summary>
        public static void FillChestLoot(Chest chest, int tileId, int chestStyle, ChestLootConfig lootCfg)
        {
            var tier = lootCfg.GetTier(tileId, chestStyle);
            if (tier == null) return;

            Array.Clear(chest.item, 0, chest.item.Length);
            int slotIndex = 0;

            // ========== 1. 必出主物品逻辑 ==========
            var mainItem = GetRandomMainItem(tier.MainItemPool);
            if (mainItem != null && slotIndex < chest.item.Length)
            {
                Item newItem = new Item();
                newItem.SetDefaults(mainItem.ItemId);
                newItem.stack = WorldGen.genRand.Next(mainItem.MinStack, mainItem.MaxStack + 1);

                // 读取全局配置，判定是否生成随机前缀
                if (lootCfg.EnableRandomPrefix)
                {
                    // 根据配置的最小/最大前缀ID随机取值，强转byte兼容prefix字段
                    int prefixRoll = WorldGen.genRand.Next(lootCfg.PrefixMinId, lootCfg.PrefixMaxId + 1);
                    newItem.prefix = (byte)prefixRoll;
                }

                chest.item[slotIndex++] = newItem;
            }

            // ========== 2. 次要分组互斥逻辑 ==========
            foreach (var group in tier.SecondaryGroups)
            {
                // 箱子格子已满，直接终止
                if (slotIndex >= chest.item.Length) break;

                // 按概率判定该分组是否生成（0~100随机）
                int rollChance = WorldGen.genRand.Next(1, 101);
                if (rollChance > group.SpawnChance)
                    continue; // 概率不命中，跳过本组

                // 组内权重随机选一件
                var pickItem = GetRandomMainItem(group.Items);
                if (pickItem == null) continue;

                // 生成物品，完全读取配置堆叠
                Item secItem = new Item();
                secItem.SetDefaults(pickItem.ItemId);
                secItem.stack = WorldGen.genRand.Next(pickItem.MinStack, pickItem.MaxStack + 1);

                // 次要物品同样读取全局前缀配置，强转byte
                if (lootCfg.EnableRandomPrefix)
                {
                    int prefixRoll = WorldGen.genRand.Next(lootCfg.PrefixMinId, lootCfg.PrefixMaxId + 1);
                    secItem.prefix = (byte)prefixRoll;
                }

                chest.item[slotIndex++] = secItem;
            }
        }
        
        /// <summary>加载箱子战利品配置</summary>
        public static bool LoadChestLootConfig()
        {
            string chestConfigPath = Path.Combine(MannequinConfigManager.ConfigFolder, "ChestLootConfig.json");
            if (!Directory.Exists(MannequinConfigManager.ConfigFolder))
            {
                Directory.CreateDirectory(MannequinConfigManager.ConfigFolder);
            }
            if (!File.Exists(chestConfigPath))
            {
                ChestLootConfig defaultCfg = new ChestLootConfig();
                string json = JsonConvert.SerializeObject(defaultCfg, Formatting.Indented);
                File.WriteAllText(chestConfigPath, json);
                TShock.Log.ConsoleInfo("[Resourcefill] 已生成 Resourcefill/ChestLootConfig.json");
            }
            try
            {
                string text = File.ReadAllText(chestConfigPath);
                ChestLootCfg = JsonConvert.DeserializeObject<ChestLootConfig>(text);
                return true;
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"箱子战利品配置读取失败：{ex.Message}");
                return false;
            }
        }
        
        /// <summary>根据权重从主物品池随机抽取1个物品</summary>
        private static LootItem GetRandomMainItem(List<LootItem> pool)
        {
            if (pool == null || pool.Count == 0) return null;
            int totalWeight = 0;
            foreach (var i in pool) totalWeight += i.Weight;
            int roll = WorldGen.genRand.Next(1, totalWeight + 1);
            int current = 0;
            foreach (var item in pool)
            {
                current += item.Weight;
                if (roll <= current) return item;
            }
            return pool[0];
        }

        /// <summary>遍历整张地图所有已存在的空箱子，按对应Style填充战利品</summary>
        /// <returns>成功填充的箱子数量</returns>
        public static int FillAllEmptyWorldChests(ChestLootConfig lootCfg)
        {
            if (lootCfg == null) return 0;
            int fillCount = 0;

            // 遍历全部箱子数组
            for (int chestIndex = 0; chestIndex < Main.chest.Length; chestIndex++)
            {
                Chest chest = Main.chest[chestIndex];
                if (chest == null) continue;

                // 判断箱子是否为空：全部格子type=0即为空箱子
                bool isEmptyChest = true;
                foreach (Item it in chest.item)
                {
                    if (it.type != 0)
                    {
                        isEmptyChest = false;
                        break;
                    }
                }
                if (!isEmptyChest) continue;

                // 获取当前箱子方块坐标
                int tileX = chest.x;
                int tileY = chest.y;
                ITile tileInterface = Main.tile[tileX, tileY];
                Tile chestTile = (Tile)tileInterface;

                // 获取箱子方块ID
                int tileId = chestTile.type;
                int chestStyle = chestTile.frameX / 36;

                // 按方块ID限制style合法区间
                if (tileId == 21)
                    chestStyle = Math.Clamp(chestStyle, 0, 51);
                else if (tileId == 467)
                    chestStyle = Math.Clamp(chestStyle, 0, 37); // 上限改为37
                else
                    continue; // 非21/467箱子跳过

                // 填充战利品，参数齐全
                FillChestLoot(chest, tileId, chestStyle, lootCfg);
                fillCount++;

                // 同步给所有在线玩家
                foreach (TSPlayer plr in TShock.Players)
                {
                    if (plr != null && plr.Active)
                        plr.SendData(PacketTypes.ChestItem, "", chestIndex);
                }
            }
            return fillCount;
        }

        
        #endregion
        
        /// <summary>反射填充模特全身装备，读取mannequin_config.json随机生成物品+前缀</summary>
        public static bool FillDisplayDollReflect(TEDisplayDoll dollObj)
        {
            if (dollObj == null) return false;
            Type dollType = dollObj.GetType();
            FieldInfo equipField = dollType.GetField("_equip", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetField);
            if (equipField == null) return false;

            Item[] equipSlots = (Item[])equipField.GetValue(dollObj);
            if (equipSlots == null) return false;

            // 清空所有槽位
            for (int i = 0; i < equipSlots.Length; i++)
            {
                equipSlots[i] = new Item();
                equipSlots[i].SetDefaults(0);
            }

            // 循环9个穿戴槽
            for (int slotIdx = 0; slotIdx < 9; slotIdx++)
            {
                var slotCfg = GetSlotConfigByIndex(slotIdx);
                if (slotCfg == null || !slotCfg.Enable) continue;

                int itemId = GetRandomItemId(slotCfg);
                if (itemId == -1) continue;

                int prefixId = GetRandomPrefix(slotCfg);
                Item item = new Item();
                item.SetDefaults(itemId);
                item.stack = 1;
                item.Prefix(prefixId);

                equipSlots[slotIdx] = item;
            }

            equipField.SetValue(dollObj, equipSlots);
            return true;
        }
        
        public static bool AddManaCrystal(int i, int j)
{
    int k = j;
    while (k < Main.maxTilesY)
    {
        ITile checkTile = Main.tile[i, k];
        if (checkTile.active() && Main.tileSolid[(int)checkTile.type])
        {
            int num = k - 1;
            // 岩浆/微光屏蔽
            if (Main.tile[i, num - 1].anyLava() || Main.tile[i - 1, num - 1].anyLava())
                return false;
            if (Main.tile[i, num - 1].anyShimmer())
                return false;
            // 四周空间检测
            if (!WorldGen.EmptyTileCheck(i - 1, i, num - 1, num, -1))
                return false;
            // 地牢墙屏蔽
            if (Main.wallDungeon[(int)Main.tile[i, num].wall])
                return false;
            // 空岛世界就近屏蔽
            if (WorldGen.skyblockWorldGen && WorldGen.IsTileNearby(i, num, 639, 50))
                return false;

            // 修复：ITile 转 Tile 实体
            Tile tile = (Tile)Main.tile[i - 1, num + 1];
            Tile tile2 = (Tile)Main.tile[i, num + 1];
            
            if (!tile.active() || !Main.tileSolid[tile.type])
                return false;
            if (!tile2.active() || !Main.tileSolid[tile2.type])
                return false;

            // 修正斜坡/半砖
            if (tile.blockType() != 0)
            {
                tile.slope(0);
                tile.halfBrick(false);
            }
            if (tile2.blockType() != 0)
            {
                tile2.slope(0);
                tile2.halfBrick(false);
            }

            // 填充完整2×2魔力水晶四格帧
            // 左上
            ITile t1 = Main.tile[i - 1, num - 1];
            t1.active(true);
            t1.type = 639;
            t1.frameX = 0;
            t1.frameY = 0;
            // 右上
            ITile t2 = Main.tile[i, num - 1];
            t2.active(true);
            t2.type = 639;
            t2.frameX = 18;
            t2.frameY = 0;
            // 左下
            ITile t3 = Main.tile[i - 1, num];
            t3.active(true);
            t3.type = 639;
            t3.frameX = 0;
            t3.frameY = 18;
            // 右下
            ITile t4 = Main.tile[i, num];
            t4.active(true);
            t4.type = 639;
            t4.frameX = 18;
            t4.frameY = 18;

            // 全服同步图格
            foreach (TSPlayer plr in TShock.Players)
            {
                if (plr != null && plr.Active)
                    plr.SendTileRect((short)(i - 1), (short)(num - 1), 2, 2);
            }
            return true;
        }
        k++;
    }
    return false;
}
        
        /// <summary>根据区域名称查找配置区域，找不到返回null</summary>
        public static GenRegion GetGenRegionByName(string regionName)
        {
            if (ChestConfig == null || ChestConfig.GenRegions == null)
                return null;
            return ChestConfig.GenRegions.FirstOrDefault(r => r.RegionName.Equals(regionName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>判断坐标是否在指定矩形区域内</summary>
        public static bool IsPointInRegion(int x, int y, GenRegion region)
        {
            if (region == null) return false;
            return x >= region.XMin && x <= region.XMax && y >= region.YMin && y <= region.YMax;
        }
    } 
}