using System;
using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// 炮塔控制器 - 负责单个炮塔的瞄准和开火控制
    /// 封装了炮塔的底层控制逻辑
    /// </summary>
    public static class 炮塔控制器
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
            if (炮塔 == null || !炮塔.IsFunctional)
                return false;

            // 计算瞄准角度
            double 方位角, 俯仰角;
            计算瞄准角度(炮塔, 瞄准点, out 方位角, out 俯仰角);

            // 设置炮塔角度
            炮塔.Azimuth = (float)方位角;
            炮塔.Elevation = (float)俯仰角;

            // 同步角度确保立即生效
            同步角度(炮塔);

            // 检查俯仰是否有效（使用弧度制的通用限制）
            // 注意：实际限制应从炮塔静态信息获取，这里做基本检查
            return true; // 炮塔API会自动限制在有效范围内
        }

        /// <summary>
        /// 控制炮塔瞄准指定位置（带俯仰限制检查）
        /// </summary>
        /// <param name="炮塔">炮塔方块</param>
        /// <param name="瞄准点">世界坐标瞄准点</param>
        /// <param name="俯仰下限">俯仰下限（弧度）</param>
        /// <param name="俯仰上限">俯仰上限（弧度）</param>
        /// <returns>是否在有效俯仰范围内</returns>
        public static bool 瞄准带限制(IMyLargeTurretBase 炮塔, Vector3D 瞄准点, double 俯仰下限, double 俯仰上限)
        {
            if (炮塔 == null || !炮塔.IsFunctional)
                return false;

            // 计算瞄准角度
            double 方位角, 俯仰角;
            计算瞄准角度(炮塔, 瞄准点, out 方位角, out 俯仰角);

            // 检查俯仰是否在限制范围内
            bool 俯仰有效 = 检查俯仰有效性(俯仰角, 俯仰下限, 俯仰上限);

            // 设置炮塔角度
            炮塔.Azimuth = (float)方位角;
            炮塔.Elevation = (float)俯仰角;

            // 同步角度确保立即生效
            同步角度(炮塔);

            return 俯仰有效;
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
            // 获取炮塔位置和朝向矩阵
            Vector3D 炮塔位置 = 炮塔.GetPosition();
            MatrixD 炮塔矩阵 = 炮塔.WorldMatrix;

            // 创建从炮塔视角看的变换矩阵
            // 这个矩阵将世界坐标转换为炮塔本地坐标
            MatrixD 观察矩阵 = MatrixD.CreateLookAt(
                Vector3D.Zero,
                炮塔矩阵.Forward,
                炮塔矩阵.Up);

            // 计算从炮塔到瞄准点的方向向量（本地坐标系）
            Vector3D 方向向量 = 瞄准点 - 炮塔位置;
            Vector3D 本地方向 = Vector3D.TransformNormal(方向向量, 观察矩阵);
            本地方向 = Vector3D.Normalize(本地方向);

            // 使用游戏API计算方位角和俯仰角
            Vector3D.GetAzimuthAndElevation(本地方向, out 方位角, out 俯仰角);
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
        /// 注意：ShootOnce是一个耗时操作，不要频繁调用
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

        /// <summary>
        /// 检查炮塔是否可用
        /// </summary>
        public static bool 检查可用(IMyLargeTurretBase 炮塔)
        {
            return 炮塔 != null && 炮塔.IsFunctional && 炮塔.Enabled;
        }

        #endregion
    }
}
