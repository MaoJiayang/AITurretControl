using System;
using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// 火控计算器 - 负责弹道计算和提前量预测
    /// 包含重力补偿、迭代求解等功能
    /// </summary>
    public class 火控计算器
    {
        #region 常量

        /// <summary>迭代收敛阈值（米）</summary>
        private const double 收敛阈值 = 0.5;

        /// <summary>最小有效距离（米）</summary>
        private const double 最小距离 = 0.01;

        /// <summary>最小有效值</summary>
        private const double 最小有效值 = 0.01;

        #endregion

        #region 字段

        /// <summary>参数管理器引用</summary>
        private 参数管理器 _参数;

        /// <summary>目标跟踪器引用</summary>
        private TargetTracker _目标跟踪器;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="参数">参数管理器</param>
        /// <param name="目标跟踪器">目标跟踪器</param>
        public 火控计算器(参数管理器 参数, TargetTracker 目标跟踪器)
        {
            _参数 = 参数;
            _目标跟踪器 = 目标跟踪器;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 计算炮塔的瞄准点（世界坐标）
        /// 综合考虑弹道提前量和重力补偿
        /// </summary>
        /// <param name="炮塔位置">炮塔世界坐标位置</param>
        /// <param name="弹速">炮弹速度（m/s）</param>
        /// <param name="受重力影响">弹药是否受重力影响</param>
        /// <param name="舰船速度">本舰速度向量</param>
        /// <param name="重力向量">重力加速度向量</param>
        /// <returns>瞄准点世界坐标</returns>
        public Vector3D 计算瞄准点(
            Vector3D 炮塔位置,
            double 弹速,
            bool 受重力影响,
            Vector3D 舰船速度,
            Vector3D 重力向量)
        {
            // 获取目标当前位置和速度
            var 目标信息 = _目标跟踪器.GetLatestTargetInfo();
            if (!目标信息.HasValue)
            {
                return 炮塔位置 + Vector3D.Forward * 100; // 无目标时返回默认方向
            }

            Vector3D 目标位置 = 目标信息.Value.Position;
            Vector3D 目标速度 = 目标信息.Value.Velocity;

            // 1. 使用线性解作为初始拦截点
            Vector3D 拦截点 = 计算线性解拦截点(炮塔位置, 目标位置, 目标速度, 舰船速度, 弹速);

            // 2. 迭代优化拦截点
            拦截点 = 迭代优化拦截点(炮塔位置, 拦截点, 弹速, 舰船速度);

            // 3. 应用重力补偿
            if (受重力影响 && 重力向量.LengthSquared() > 最小有效值)
            {
                double 距离 = Vector3D.Distance(炮塔位置, 拦截点);
                double 飞行时间 = 距离 / 弹速;
                Vector3D 重力补偿 = 计算重力补偿(飞行时间, 重力向量);
                拦截点 -= 重力补偿;
            }

            return 拦截点;
        }

        /// <summary>
        /// 计算线性解拦截点（初始估计）
        /// 使用二次方程求解拦截时间
        /// </summary>
        public Vector3D 计算线性解拦截点(
            Vector3D 炮塔位置,
            Vector3D 目标位置,
            Vector3D 目标速度,
            Vector3D 舰船速度,
            double 弹速)
        {
            // 相对位置和速度
            Vector3D 相对位置 = 目标位置 - 炮塔位置;
            Vector3D 相对速度 = 目标速度 - 舰船速度;

            // 解二次方程计算拦截时间
            // |相对位置 + 相对速度*t| = 弹速 * t
            // 展开：|相对速度|²*t² + 2*(相对位置·相对速度)*t + |相对位置|² = 弹速²*t²
            // 整理：(|相对速度|² - 弹速²)*t² + 2*(相对位置·相对速度)*t + |相对位置|² = 0
            double a = 相对速度.LengthSquared() - 弹速 * 弹速;
            double b = 2 * Vector3D.Dot(相对位置, 相对速度);
            double c = 相对位置.LengthSquared();

            // a≈0时表示目标速度接近武器弹速，特殊处理
            if (Math.Abs(a) < 最小有效值)
            {
                // 简化为线性方程：b*t + c = 0
                if (Math.Abs(b) > 最小有效值)
                {
                    double t = -c / b;
                    if (t > 0)
                    {
                        return 目标位置 + 目标速度 * t - 舰船速度 * t;
                    }
                }
                return 目标位置;
            }

            double 判别式 = b * b - 4 * a * c;

            if (判别式 >= 0)
            {
                // 有实数解，计算拦截时间
                double 判别式开方 = Math.Sqrt(判别式);
                double t1 = (-b + 判别式开方) / (2 * a);
                double t2 = (-b - 判别式开方) / (2 * a);

                // 选择有效的正解
                double 拦截时间 = double.NaN;
                if (t1 > 0 && t2 > 0)
                    拦截时间 = Math.Min(t1, t2);
                else if (t1 > 0)
                    拦截时间 = t1;
                else if (t2 > 0)
                    拦截时间 = t2;

                if (!double.IsNaN(拦截时间) && 拦截时间 > 0)
                {
                    // 使用线性解
                    return 目标位置 + 目标速度 * 拦截时间 - 舰船速度 * 拦截时间;
                }
            }

            // 无解时返回当前目标位置
            return 目标位置;
        }

        /// <summary>
        /// 迭代优化拦截点
        /// 使用目标跟踪器的预测功能进行迭代优化
        /// </summary>
        public Vector3D 迭代优化拦截点(
            Vector3D 炮塔位置,
            Vector3D 初始拦截点,
            double 弹速,
            Vector3D 舰船速度)
        {
            Vector3D 拦截点 = 初始拦截点;
            int 跳帧 = _参数.火控更新跳帧;

            for (int i = 0; i < _参数.弹道迭代次数; i++)
            {
                // 计算当前拦截点需要的飞行时间
                double 距离 = Vector3D.Distance(炮塔位置, 拦截点);
                double 飞行时间 = 距离 / 弹速;

                // 预测目标在未来位置（考虑火控更新延迟）
                long 预测时间ms = (long)(飞行时间 * 1000) + 跳帧 * 17;
                var 目标预测 = _目标跟踪器.PredictFutureTargetInfo(预测时间ms, false);

                // 参考系变换：计算舰船在飞行时间内的位移
                Vector3D 舰船位移 = 舰船速度 * 飞行时间;

                // 新拦截点 = 目标未来位置 - 舰船位移
                Vector3D 新拦截点 = 目标预测.Position - 舰船位移;

                // 检查预测收敛条件
                if (Vector3D.Distance(拦截点, 新拦截点) < 收敛阈值)
                    break;

                拦截点 = 新拦截点;
            }

            return 拦截点;
        }

        /// <summary>
        /// 计算重力补偿偏移量
        /// 补偿弹道下坠导致的偏差
        /// </summary>
        public Vector3D 计算重力补偿(double 飞行时间, Vector3D 重力向量)
        {
            // 补偿量 = 0.5 * g * t²
            // 重力导致弹道下坠，需要向上瞄（即瞄准点需要向重力反方向偏移）
            return 0.5 * 重力向量 * 飞行时间 * 飞行时间;
        }

        /// <summary>
        /// 计算平行偏差修正
        /// 用于非代表炮塔基于代表炮塔的计算结果进行位置修正
        /// </summary>
        public Vector3D 计算平行偏差修正(
            Vector3D 代表瞄准点,
            Vector3D 代表位置,
            Vector3D 当前位置,
            double 弹速,
            Vector3D 舰船速度,
            Vector3D 目标速度)
        {
            // 计算炮塔之间的位置差
            Vector3D 位置差 = 当前位置 - 代表位置;

            // 如果位置差很小，不需要修正
            if (位置差.LengthSquared() < _参数.平行偏差修正阈值 * _参数.平行偏差修正阈值)
            {
                return 代表瞄准点;
            }

            // 计算代表炮塔的飞行时间
            double 代表距离 = Vector3D.Distance(代表位置, 代表瞄准点);
            double 代表飞行时间 = 代表距离 / 弹速;

            // 计算当前炮塔到瞄准点的距离差
            double 当前距离 = Vector3D.Distance(当前位置, 代表瞄准点);
            double 当前飞行时间 = 当前距离 / 弹速;

            // 飞行时间差
            double 时间差 = 当前飞行时间 - 代表飞行时间;

            // 根据时间差调整瞄准点
            // 目标在时间差内的移动 - 本舰在时间差内的移动
            Vector3D 调整量 = (目标速度 - 舰船速度) * 时间差;

            return 代表瞄准点 + 调整量;
        }

        /// <summary>
        /// 检查目标是否在射程内
        /// </summary>
        public bool 检查射程(Vector3D 炮塔位置, Vector3D 目标位置, double 最大射程)
        {
            double 距离平方 = (目标位置 - 炮塔位置).LengthSquared();
            return 距离平方 <= 最大射程 * 最大射程;
        }

        #endregion
    }
}
