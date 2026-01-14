using System;
using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// 炮塔控制器 - 负责单个炮塔的瞄准和开火控制
    /// 封装了炮塔的底层控制逻辑
    /// </summary>
    public class 炮塔控制器
    {
        #region 公共方法

        /// <summary>
        /// 控制炮塔瞄准指定位置
        /// 使用直接设置方位角和俯仰角的方式实现瞬间瞄准
        /// </summary>
        /// <param name="炮塔">炮塔方块</param>
        /// <param name="瞄准点">世界坐标瞄准点</param>
        /// <returns>是否在有效俯仰范围内</returns>
        public static bool 瞄准(IMyLargeTurretBase 炮塔, Vector3D 瞄准点)
        {
            // TODO: 实现炮塔瞄准控制
            // 参考AdvancedTurretControl中的TurretAim方法
            // 1. 计算从炮塔到瞄准点的方向向量
            // 2. 转换为本地坐标系
            // 3. 计算方位角和俯仰角
            // 4. 设置炮塔角度
            // 5. 同步角度
            // 6. 返回俯仰是否有效

            throw new NotImplementedException("炮塔瞄准 - 待实现");
        }

        /// <summary>
        /// 计算炮塔瞄准所需的方位角和俯仰角
        /// </summary>
        /// <param name="炮塔">炮塔方块</param>
        /// <param name="瞄准点">世界坐标瞄准点</param>
        /// <param name="方位角">输出：方位角（弧度）</param>
        /// <param name="俯仰角">输出：俯仰角（弧度）</param>
        public static void 计算瞄准角度(
            IMyLargeTurretBase 炮塔,
            Vector3D 瞄准点,
            out double 方位角,
            out double 俯仰角)
        {
            // TODO: 实现角度计算
            // 参考AdvancedTurretControl中的角度计算逻辑

            throw new NotImplementedException("计算瞄准角度 - 待实现");
        }

        /// <summary>
        /// 检查目标俯仰角是否在炮塔限制范围内
        /// </summary>
        /// <param name="俯仰角">目标俯仰角（弧度）</param>
        /// <param name="俯仰下限">俯仰下限（弧度）</param>
        /// <param name="俯仰上限">俯仰上限（弧度）</param>
        /// <returns>是否在有效范围内</returns>
        public static bool 检查俯仰有效性(double 俯仰角, double 俯仰下限, double 俯仰上限)
        {
            return 俯仰角 >= 俯仰下限 && 俯仰角 <= 俯仰上限;
        }

        /// <summary>
        /// 控制炮塔开火一次
        /// </summary>
        /// <param name="炮塔">炮塔方块</param>
        public static void 开火(IMyLargeTurretBase 炮塔)
        {
            if (炮塔 != null && 炮塔.IsFunctional && 炮塔.Enabled)
            {
                炮塔.ShootOnce();
            }
        }

        /// <summary>
        /// 同步炮塔角度
        /// 确保设置的角度立即生效
        /// </summary>
        /// <param name="炮塔">炮塔方块</param>
        public static void 同步角度(IMyLargeTurretBase 炮塔)
        {
            if (炮塔 != null)
            {
                炮塔.SyncAzimuth();
                炮塔.SyncElevation();
            }
        }

        /// <summary>
        /// 重置炮塔到默认瞄准状态
        /// </summary>
        /// <param name="炮塔">炮塔方块</param>
        public static void 重置(IMyLargeTurretBase 炮塔)
        {
            if (炮塔 != null)
            {
                炮塔.ResetTargetingToDefault();
            }
        }

        #endregion
    }
}
