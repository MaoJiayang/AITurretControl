using Sandbox.ModAPI.Ingame;
using System;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// 视线判定器 - 使用3D DDA算法检测炮塔到目标点的视线是否被友方方块阻挡
    /// 
    /// 算法示例：从 (0,0,0) 射向 (3,1,2)
    /// - 方向 = (3,1,2)
    /// - tDelta = (1/3, 1/1, 1/2) = (0.333, 1.0, 0.5) ← 每前进1个体素，参数t的增量
    /// - tMax初始 = (0.167, 0.5, 0.25) ← 到达下一个边界的参数值
    /// - 遍历顺序：(0,0,0) → (1,0,0) → (1,0,1) → (2,0,1) → (2,1,1) → (3,1,1) → (3,1,2)
    /// </summary>
    public class 视线判定器
    {
        /// <summary>
        /// 友方网格 - 用于检查方块是否存在
        /// </summary>
        private IMyCubeGrid 友方网格;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="参考方块">参考终端方块（通常是Me），从中获取所在网格</param>
        public 视线判定器(IMyTerminalBlock 参考方块)
        {
            if (参考方块 == null)
                throw new ArgumentNullException("参考方块不能为null");
            
            this.友方网格 = 参考方块.CubeGrid;
        }

        /// <summary>
        /// 判定视线是否畅通（使用3D DDA算法）
        /// </summary>
        /// <param name="炮塔位置">炮塔所在的体素坐标</param>
        /// <param name="瞄准点位置">目标瞄准点的体素坐标</param>
        /// <returns>true表示视线畅通，false表示被友方方块阻挡</returns>
        public bool 判定视线畅通(Vector3I 炮塔位置, Vector3I 瞄准点位置)
        {
            // 计算射线方向
            Vector3I 方向 = 瞄准点位置 - 炮塔位置;
            
            // 如果起点和终点相同，直接返回true
            if (方向 == Vector3I.Zero)
                return true;

            // 当前遍历位置
            Vector3I 当前位置 = 炮塔位置;

            // 计算每个轴的步进方向（-1, 0, 或 1）
            int 步进X = Math.Sign(方向.X);
            int 步进Y = Math.Sign(方向.Y);
            int 步进Z = Math.Sign(方向.Z);

            // 计算tDelta：射线在各轴方向前进一个体素需要的参数增量
            // 如果某个方向没有移动，则设为无穷大
            // 公式：1.0 / |方向分量| （1.0是体素的单位长度，是常数）
            // 示例：方向(3,1,2) → tDelta = (1/3, 1/1, 1/2) = (0.333, 1.0, 0.5)
            double tDeltaX = (步进X != 0) ? Math.Abs(1.0 / 方向.X) : double.MaxValue;
            double tDeltaY = (步进Y != 0) ? Math.Abs(1.0 / 方向.Y) : double.MaxValue;
            double tDeltaZ = (步进Z != 0) ? Math.Abs(1.0 / 方向.Z) : double.MaxValue;

            // 计算tMax：射线到达下一个体素边界的参数值
            // 0.5是从体素中心到边界的距离（半个体素，是常数）
            // 示例：tDelta(0.333, 1.0, 0.5) × 0.5 → tMax初始 = (0.167, 0.5, 0.25)
            double tMaxX = (步进X != 0) ? tDeltaX * 0.5 : double.MaxValue;
            double tMaxY = (步进Y != 0) ? tDeltaY * 0.5 : double.MaxValue;
            double tMaxZ = (步进Z != 0) ? tDeltaZ * 0.5 : double.MaxValue;

            // 获取网格边界用于边界检查
            Vector3I 网格最大值 = 友方网格.Max;
            Vector3I 网格最小值 = 友方网格.Min;

            // DDA主循环 - 遍历射线经过的每个体素
            // 使用while循环，当射线穿出网格边界时自动停止
            while (true)
            {
                // 检查当前位置（跳过起始位置）
                if (当前位置 != 炮塔位置)
                {
                    // 如果到达目标点，视线畅通
                    if (当前位置 == 瞄准点位置)
                        return true;

                    // 检查当前位置是否有友方方块阻挡
                    // 使用游戏API直接检查该位置是否存在方块
                    if (友方网格.CubeExists(当前位置))
                    {
                        // 遇到阻挡，视线不通
                        return false;
                    }
                }

                // 选择tMax最小的方向前进
                if (tMaxX < tMaxY)
                {
                    if (tMaxX < tMaxZ)
                    {
                        // X方向最近
                        当前位置.X += 步进X;
                        tMaxX += tDeltaX;
                    }
                    else
                    {
                        // Z方向最近
                        当前位置.Z += 步进Z;
                        tMaxZ += tDeltaZ;
                    }
                }
                else
                {
                    if (tMaxY < tMaxZ)
                    {
                        // Y方向最近
                        当前位置.Y += 步进Y;
                        tMaxY += tDeltaY;
                    }
                    else
                    {
                        // Z方向最近
                        当前位置.Z += 步进Z;
                        tMaxZ += tDeltaZ;
                    }
                }

                // 检查是否超出网格边界 - 如果超出则视线畅通（已离开友方网格）
                if (当前位置.X < 网格最小值.X || 当前位置.X > 网格最大值.X ||
                    当前位置.Y < 网格最小值.Y || 当前位置.Y > 网格最大值.Y ||
                    当前位置.Z < 网格最小值.Z || 当前位置.Z > 网格最大值.Z)
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// 更新参考网格（用于动态切换检查的网格）
        /// </summary>
        /// <param name="新参考方块">新的参考终端方块</param>
        public void 更新参考网格(IMyTerminalBlock 新参考方块)
        {
            if (新参考方块 == null)
                throw new ArgumentNullException("新参考方块不能为null");
            
            this.友方网格 = 新参考方块.CubeGrid;
        }
    }
}
