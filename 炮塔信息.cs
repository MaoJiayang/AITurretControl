using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// 弹药类型 - 自带弹道属性的弹药类型定义
    /// 同一种弹药类型具有相同的弹速、射程和重力影响特性
    /// </summary>
    public class 弹药类型
    {
        /// <summary>弹药名称</summary>
        public string 名称 { get; private set; }
        
        /// <summary>弹速 (m/s)</summary>
        public double 弹速 { get; private set; }
        
        /// <summary>最大射程 (m)</summary>
        public double 最大射程 { get; private set; }
        
        /// <summary>是否受重力影响</summary>
        public bool 受重力影响 { get; private set; }

        private 弹药类型(string 名称, double 弹速, double 最大射程, bool 受重力影响)
        {
            this.名称 = 名称;
            this.弹速 = 弹速;
            this.最大射程 = 最大射程;
            this.受重力影响 = 受重力影响;
        }

        // 预定义的弹药类型
        public static readonly 弹药类型 加特林弹药 = new 弹药类型("加特林弹药", 800, 800, true);
        public static readonly 弹药类型 火箭弹药 = new 弹药类型("火箭弹药", 200, 800, false);
        public static readonly 弹药类型 火炮弹药 = new 弹药类型("火炮弹药", 500, 2000, true);
        public static readonly 弹药类型 突击炮弹药 = new 弹药类型("突击炮弹药", 500, 1400, true);
        public static readonly 弹药类型 室内炮弹药 = new 弹药类型("室内炮弹药", 600, 600, true);
        public static readonly 弹药类型 机炮弹药 = new 弹药类型("机炮弹药", 800, 800, true);

        public override string ToString()
        {
            return 名称;
        }
    }

    /// <summary>
    /// 炮塔静态信息 - 存储炮塔类型的固定属性
    /// 这些信息在游戏中不会改变，可以缓存使用
    /// 弹速、最大射程、受重力影响等属性自动从弹药类型注入
    /// </summary>
    public struct 炮塔静态信息
    {
        public readonly 弹药类型 弹药类型;
        public readonly double 弹速;           // m/s (从弹药类型自动注入)
        public readonly double 最大射程;       // m (从弹药类型自动注入)
        public readonly double 俯仰下限;       // 弧度，负值表示向下
        public readonly double 俯仰上限;       // 弧度
        public readonly double 射击速率;       // 发/秒
        public readonly int 弹匣容量;          // 发
        public readonly double 装填时间;       // 秒
        public readonly bool 受重力影响;       // 弹药是否受重力影响 (从弹药类型自动注入)
        public readonly bool 是火炮类;         // 是否为火炮类武器（用于轮射判断）

        public 炮塔静态信息(
            弹药类型 弹药类型,
            double 俯仰下限度,
            double 俯仰上限度,
            double 射击速率,
            int 弹匣容量,
            double 装填时间,
            bool 是火炮类)
        {
            this.弹药类型 = 弹药类型;
            // 从弹药类型自动注入弹道属性
            this.弹速 = 弹药类型.弹速;
            this.最大射程 = 弹药类型.最大射程;
            this.受重力影响 = 弹药类型.受重力影响;
            // 炮塔特定属性
            this.俯仰下限 = 俯仰下限度 * Math.PI / 180.0;
            this.俯仰上限 = 俯仰上限度 * Math.PI / 180.0;
            this.射击速率 = 射击速率;
            this.弹匣容量 = 弹匣容量;
            this.装填时间 = 装填时间;
            this.是火炮类 = 是火炮类;
        }
    }

    /// <summary>
    /// 炮塔信息查询器 - 根据炮塔方块获取静态信息
    /// 使用静态字典缓存，避免重复创建
    /// </summary>
    public static class 炮塔信息查询器
    {
        // 按SubtypeId缓存的炮塔信息
        private static readonly Dictionary<string, 炮塔静态信息> _信息缓存;

        // 默认信息（用于未知类型）
        private static readonly 炮塔静态信息 _默认加特林信息;
        private static readonly 炮塔静态信息 _默认火箭信息;

        /// <summary>
        /// 静态构造函数 - 初始化所有已知炮塔类型的信息
        /// </summary>
        static 炮塔信息查询器()
        {
            _信息缓存 = new Dictionary<string, 炮塔静态信息>();

            // ============ 加特林炮塔系列 ============
            // 大型加特林炮塔（原皮和换皮）
            var 大型加特林 = new 炮塔静态信息(
                弹药类型: 弹药类型.加特林弹药,
                俯仰下限度: -43,
                俯仰上限度: 90,
                射击速率: 10,
                弹匣容量: 140,
                装填时间: 4,
                是火炮类: false
            );
            _信息缓存["LargeGatlingTurretReskin"] = 大型加特林;
            _默认加特林信息 = 大型加特林; // 原皮大型加特林使用此信息

            // 小型加特林炮塔
            var 小型加特林 = new 炮塔静态信息(
                弹药类型: 弹药类型.加特林弹药,
                俯仰下限度: -10,
                俯仰上限度: 90,
                射击速率: 10,
                弹匣容量: 140,
                装填时间: 6,
                是火炮类: false
            );
            _信息缓存["SmallGatlingTurret"] = 小型加特林;
            _信息缓存["SmallGatlingTurretReskin"] = 小型加特林;

            // ============ 火箭炮塔系列 ============
            // 大型火箭炮塔（原皮和换皮）
            var 大型火箭 = new 炮塔静态信息(
                弹药类型: 弹药类型.火箭弹药,
                俯仰下限度: -58,
                俯仰上限度: 90,
                射击速率: 1.5,
                弹匣容量: 6,
                装填时间: 4,
                是火炮类: false
            );
            _信息缓存["LargeMissileTurretReskin"] = 大型火箭;
            _默认火箭信息 = 大型火箭; // 原皮大型火箭使用此信息

            // 小型火箭炮塔
            var 小型火箭 = new 炮塔静态信息(
                弹药类型: 弹药类型.火箭弹药,
                俯仰下限度: -8,
                俯仰上限度: 90,
                射击速率: 1.5,
                弹匣容量: 2,
                装填时间: 6,
                是火炮类: false
            );
            _信息缓存["SmallMissileTurret"] = 小型火箭;
            _信息缓存["SmallMissileTurretReskin"] = 小型火箭;

            // ============ 火炮炮塔 ============
            var 火炮 = new 炮塔静态信息(
                弹药类型: 弹药类型.火炮弹药,
                俯仰下限度: -15,
                俯仰上限度: 60,
                射击速率: 1.33,
                弹匣容量: 2,
                装填时间: 12,
                是火炮类: true  // 火炮类，参与轮射
            );
            _信息缓存["LargeCalibreTurret"] = 火炮;

            // ============ 突击加农炮炮塔 ============
            var 大型突击炮 = new 炮塔静态信息(
                弹药类型: 弹药类型.突击炮弹药,
                俯仰下限度: -20,
                俯仰上限度: 75,
                射击速率: 3,
                弹匣容量: 2,
                装填时间: 6,
                是火炮类: true  // 火炮类，参与轮射
            );
            _信息缓存["LargeBlockMediumCalibreTurret"] = 大型突击炮;

            var 小型突击炮 = new 炮塔静态信息(
                弹药类型: 弹药类型.突击炮弹药,
                俯仰下限度: -10,
                俯仰上限度: 50,
                射击速率: 0.167, // 1发/6秒
                弹匣容量: 1,
                装填时间: 6,
                是火炮类: true
            );
            _信息缓存["SmallBlockMediumCalibreTurret"] = 小型突击炮;

            // ============ 室内炮塔 ============
            var 室内炮塔 = new 炮塔静态信息(
                弹药类型: 弹药类型.室内炮弹药,
                俯仰下限度: -76,
                俯仰上限度: 90,
                射击速率: 10,
                弹匣容量: int.MaxValue, // 无限弹匣
                装填时间: 0,
                是火炮类: false
            );
            _信息缓存["LargeInteriorTurret"] = 室内炮塔;

            // ============ 机炮炮塔 ============
            var 机炮炮塔 = new 炮塔静态信息(
                弹药类型: 弹药类型.机炮弹药,
                俯仰下限度: -10,
                俯仰上限度: 90,
                射击速率: 2.5,
                弹匣容量: 16,
                装填时间: 4,
                是火炮类: false
            );
            _信息缓存["AutoCannonTurret"] = 机炮炮塔;
        }

        /// <summary>
        /// 获取炮塔的静态信息
        /// </summary>
        /// <param name="炮塔">炮塔方块</param>
        /// <returns>炮塔静态信息</returns>
        public static 炮塔静态信息 获取炮塔信息(IMyLargeTurretBase 炮塔)
        {
            if (炮塔 == null)
                return _默认加特林信息;

            string subtypeId = 炮塔.BlockDefinition.SubtypeId;

            // 首先尝试从缓存中查找
            炮塔静态信息 信息;
            if (!string.IsNullOrEmpty(subtypeId) && _信息缓存.TryGetValue(subtypeId, out 信息))
            {
                return 信息;
            }

            // SubtypeId为空或未找到，通过类型判断
            // 注意：必须先判断子类，再判断基类
            if (炮塔 is IMyLargeGatlingTurret)
            {
                return _默认加特林信息;
            }
            else if (炮塔 is IMyLargeMissileTurret)
            {
                return _默认火箭信息;
            }

            // 未知类型，返回默认加特林信息
            return _默认加特林信息;
        }

        /// <summary>
        /// 获取炮塔的分组键（用于聚类分组）
        /// 相同弹药类型的炮塔使用相同的火控参数
        /// </summary>
        /// <param name="炮塔">炮塔方块</param>
        /// <returns>分组键字符串</returns>
        public static string 获取分组键(IMyLargeTurretBase 炮塔)
        {
            var 信息 = 获取炮塔信息(炮塔);
            // 使用弹药类型作为分组键，同弹药的炮塔可以共享火控计算
            return 信息.弹药类型.ToString();
        }
    }

    /// <summary>
    /// 炮塔运行时信息 - 存储单个炮塔的动态状态
    /// </summary>
    public class 炮塔运行时信息
    {
        /// <summary>炮塔方块引用</summary>
        public IMyLargeTurretBase 炮塔方块 { get; private set; }

        /// <summary>炮塔静态信息（缓存，避免重复查询）</summary>
        public 炮塔静态信息 静态信息 { get; private set; }

        /// <summary>分组键（用于聚类）</summary>
        public string 分组键 { get; private set; }

        /// <summary>是否为聚类代表炮塔</summary>
        public bool 是代表炮塔 { get; set; }

        /// <summary>所属聚类的代表炮塔（如果自己不是代表）</summary>
        public 炮塔运行时信息 代表炮塔 { get; set; }

        /// <summary>上次开火时间（帧计数）</summary>
        public int 上次开火帧 { get; set; }

        /// <summary>当前弹匣剩余弹药</summary>
        public int 当前弹匣余量 { get; set; }

        /// <summary>是否正在装填</summary>
        public bool 正在装填 { get; set; }

        /// <summary>装填开始帧</summary>
        public int 装填开始帧 { get; set; }

        /// <summary>轮射序号（用于轮射控制）</summary>
        public int 轮射序号 { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public 炮塔运行时信息(IMyLargeTurretBase 炮塔)
        {
            炮塔方块 = 炮塔;
            静态信息 = 炮塔信息查询器.获取炮塔信息(炮塔);
            分组键 = 炮塔信息查询器.获取分组键(炮塔);
            是代表炮塔 = false;
            代表炮塔 = null;
            上次开火帧 = -9999;
            当前弹匣余量 = 静态信息.弹匣容量;
            正在装填 = false;
            装填开始帧 = 0;
            轮射序号 = 0;
        }

        /// <summary>
        /// 获取炮塔世界坐标位置
        /// </summary>
        public Vector3D 获取位置()
        {
            return 炮塔方块.GetPosition();
        }

        /// <summary>
        /// 获取炮塔前向方向（发射方向）
        /// </summary>
        public Vector3D 获取前向()
        {
            return 炮塔方块.WorldMatrix.Forward;
        }

        /// <summary>
        /// 检查炮塔是否可用（功能正常且启用）
        /// </summary>
        public bool 是否可用()
        {
            return 炮塔方块 != null && 炮塔方块.IsFunctional;
        }
    }
}
