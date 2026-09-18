# Resoucefill 资源填充

- 作者: 唉唉有
- 出处: 修改自replenresouce
资源自动补货工具，支持手动 / 定时生成箱子、陶罐、生命水晶、魔力水晶；批量填充模特战利品、填充原生空箱子、清理空箱子，并且支持**自定义多矩形执行区域**，指令可限定在指定地图范围内执行操作。

Resourcefill/
├─ main_config.json        # 主配置（自动补货开关、数量、生成区域列表）
├─ chest_config.json       # 宝箱战利品池配置（支持 Remark 备注）
└─ mannequin_config.json   # 模特穿戴战利品配置

## 指令

指令	简写	功能
/rf help	—	查看帮助文档
/rf reload	—	热重载全部 JSON 配置
/rf chests 20	—	全图生成 20 个宝箱
/rf pots 30	—	全图生成 30 个陶罐
/rf lifecrystals 15	—	全图生成 15 个生命水晶
/rf manacrystal 20	—	全图生成 20 个魔力水晶
/rf fillmannequin	/rf fm	填充全图所有展示模特
/rf exportmannequin	/rf em	读取模特，导出模特配置文件
/rf fillchest	—	填充地图原生空箱子
/rf testchest 5	—	生成测试用自定义宝箱


## 功能清单

1. **手动生成资源**：宝箱、陶罐、生命水晶、魔力水晶
2. **批量填充展示模特**：读取战利品配置自动穿戴装备
3. **导出模特配置**：读取地图模特装备生成配置模板
4. **填充原生空箱子**：给地图原有空箱子随机补货
5. **清理全图空箱子**：一键删除没有物品的空宝箱
6. **自动定时补货**：服务端后台周期刷新资源
7. **多区域限定执行**：在配置文件自定义多个矩形区域，指令前缀指定区域执行，不填区域默认全地图
8. **保护区权限控制**：开关控制是否在 TShock 保护区域内生成资源

## 配置
> 配置文件位置：tshock/Resoucefill/
main_config.json 核心配置说明
"DebugLog": false,
"AutomaticallRefill": true,开启 / 关闭后台自动补货定时器
"AutoRefillTimerInMinutes": 10,自动补货间隔（分钟）
"GenerateInProtectedAreas": true = 允许在领地保护区域生成资源
"GenRegions": [
    {
      "RegionName": "区域1",区域名称（指令使用，大小写不敏感）
      "XMin": 40,矩形左上角坐标
      "YMin": 162,
      "XMax": 8360,矩形右下角坐标
      "YMax": 2360
    }
]

--------
mannequin_config.json
## mannequin_config.json 字段解释

- `SetName`：套装名称
- `Remark`：自定义备注文字，仅用于维护查看
- `SpawnChance`：这套装备被随机选中的概率（0~1）
- `Slot`：模特穿戴槽位
- `ItemId`：物品 ID
- `Stack`：物品数量（模特固定为 1）
- `Prefix`：附魔前缀 ID，0 = 无附魔
{
  "MannequinSets": [
    {
      "SetName": "战士套装",
      "Remark": "近战肉前全套盔甲+配饰",
      "SpawnChance": 0.4,
      "Items": [
        {
          "Slot": "Head",
          "ItemId": 124,
          "Stack": 1,
          "Prefix": 0,
          "Remark": "钴头盔"
        },
        {
          "Slot": "Body",
          "ItemId": 125,
          "Stack": 1,
          "Prefix": 0,
          "Remark": "钴胸甲"
        },
        {
          "Slot": "Legs",
          "ItemId": 126,
          "Stack": 1,
          "Prefix": 0,
          "Remark": "钴护腿"
        },
        {
          "Slot": "Accessory1",
          "ItemId": 509,
          "Stack": 1,
          "Prefix": 0,
          "Remark": "疾风靴"
        },
        {
          "Slot": "Weapon",
          "ItemId": 757,
          "Stack": 1,
          "Prefix": 0,
          "Remark": "永夜之刃"
        }
      ]
    },
    {
      "SetName": "法师套装",
      "Remark": "魔法输出套装",
      "SpawnChance": 0.35,
      "Items": [
        {
          "Slot": "Head",
          "ItemId": 134,
          "Stack": 1,
          "Prefix": 0,
          "Remark": "丛林帽"
        },
        {
          "Slot": "Body",
          "ItemId": 135,
          "Stack": 1,
          "Prefix": 0,
          "Remark": "丛林长袍"
        },
        {
          "Slot": "Legs",
          "ItemId": 136,
          "Stack": 1,
          "Prefix": 0,
          "Remark": "丛林裤"
        },
        {
          "Slot": "Weapon",
          "ItemId": 117,
          "Stack": 1,
          "Prefix": 0,
          "Remark": "恶魔镰刀"
        }
      ]
    }
  ]
}
------------
chest_config.json
1. `ChestGroups`：战利品分组池，多组互斥 / 按概率生成
2. `GroupName`：分组标识名
3. `Remark`：人工备注，**程序不参与逻辑运算**
4. `SpawnChance`：该分组的生成概率 `0~1`
5. `MinStack / MaxStack`：这个宝箱最多最少刷几件物品
6. `ItemPool[]`：物品列表
   - `ItemId`：泰拉物品 ID（纯数字，禁止前导 0）
   - `Weight`：权重，数值越大越容易抽中
   - `MinStack / MaxStack`：该物品堆叠随机区间
7. `StylePool`：宝箱方块样式池，控制生成什么外观的箱子
{
  "ChestGroups": [
    {
      "GroupName": "普通地下宝箱",
      "Remark": "地表下方普通木箱战利品组",
      "SpawnChance": 0.65,
      "MinStack": 1,
      "MaxStack": 4,
      "ItemPool": [
        {
          "ItemId": 22,
          "Weight": 10,
          "MinStack": 5,
          "MaxStack": 15,
          "Remark": "银币"
        },
        {
          "ItemId": 206,
          "Weight": 3,
          "MinStack": 1,
          "MaxStack": 1,
          "Remark": "金属探测器"
        },
        {
          "ItemId": 99,
          "Weight": 6,
          "MinStack": 10,
          "MaxStack": 30,
          "Remark": "火把"
        }
      ]
    },
    {
      "GroupName": "金矿宝箱",
      "Remark": "稀有高级战利品池，低概率刷出",
      "SpawnChance": 0.25,
      "MinStack": 2,
      "MaxStack": 5,
      "ItemPool": [
        {
          "ItemId": 509,
          "Weight": 2,
          "MinStack": 1,
          "MaxStack": 1,
          "Remark": "疾风靴"
        },
        {
          "ItemId": 110,
          "Weight": 8,
          "MinStack": 15,
          "MaxStack": 30,
          "Remark": "金锭"
        }
      ]
    }
  ],
  "StylePool": [
    {
      "TileId": 21,
      "Style": 0,
      "Weight": 8,
      "Remark": "普通木箱"
    },
    {
      "TileId": 21,
      "Style": 1,
      "Weight": 2,
      "Remark": "金宝箱样式"
    }
  ]
}


