using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
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
        /// 炮塔方块位置缓存 - 存储所有已缓存炮塔占据的体素坐标
        /// </summary>
        private HashSet<Vector3I> 炮塔方块缓存;

        /// <summary>
        /// 已缓存炮塔集合 - 记录已经构建过缓存的炮塔EntityId
        /// </summary>
        private HashSet<long> 已缓存炮塔集合;

        /// <summary>
        /// 已知方块位置缓存 - 存储已知存在方块的体素坐标，用于减少CubeExists调用
        /// </summary>
        private HashSet<Vector3I> 已知方块位置缓存;

        /// <summary>
        /// 已知空位置缓存 - 存储已知不存在方块的体素坐标，用于减少CubeExists调用
        /// </summary>
        private HashSet<Vector3I> 已知空位置缓存;

        /// <summary>
        /// 构造函数
        /// </summary>
        public 视线判定器()
        {
            炮塔方块缓存 = new HashSet<Vector3I>();
            已缓存炮塔集合 = new HashSet<long>();
            已知方块位置缓存 = new HashSet<Vector3I>();
            已知空位置缓存 = new HashSet<Vector3I>();
        }

        /// <summary>
        /// 判定视线是否畅通（使用3D DDA算法）
        /// </summary>
        /// <param name="炮塔">炮塔方块</param>
        /// <param name="瞄准点位置">目标瞄准点的世界坐标</param>
        /// <returns>true表示视线畅通，false表示被友方方块阻挡</returns>
        public bool 判定视线畅通(IMyLargeTurretBase 炮塔, Vector3D 瞄准点位置, Action<string> 输出 = null)
        {
            Vector3I 炮塔体素位置 = 炮塔.Position;
            Vector3I 瞄准点体素位置 = 炮塔.CubeGrid.WorldToGridInteger(瞄准点位置);

            // 构建炮塔方块位置缓存
            构建炮塔缓存(炮塔);

            return 判定视线畅通(炮塔体素位置, 瞄准点体素位置, 炮塔.CubeGrid, 输出);
        }

        /// <summary>
        /// 构建炮塔占据的所有方块位置缓存
        /// </summary>
        /// <param name="炮塔">炮塔方块</param>
        private void 构建炮塔缓存(IMyLargeTurretBase 炮塔)
        {
            // 检查该炮塔是否已经被缓存过
            long 炮塔ID = 炮塔.EntityId;
            if (已缓存炮塔集合.Contains(炮塔ID))
            {
                // 已缓存，直接返回
                return;
            }

            // 未缓存，添加到已缓存集合
            已缓存炮塔集合.Add(炮塔ID);

            // 获取炮塔方块的边界
            Vector3I min = 炮塔.Min;
            Vector3I max = 炮塔.Max;

            // 遍历炮塔占据的所有体素位置并添加到缓存
            for (int x = min.X; x <= max.X; x++)
            {
                for (int y = min.Y; y <= max.Y; y++)
                {
                    for (int z = min.Z; z <= max.Z; z++)
                    {
                        Vector3I 位置 = new Vector3I(x, y, z);
                        炮塔方块缓存.Add(位置);
                        已知方块位置缓存.Add(位置); // 同时添加到已知方块缓存
                    }
                }
            }
        }
        /// <summary>
        /// 判定视线是否畅通（使用3D DDA算法）
        /// </summary>
        /// <param name="炮塔位置">炮塔所在的体素坐标</param>
        /// <param name="瞄准点位置">目标瞄准点的体素坐标</param>
        /// <returns>true表示视线畅通，false表示被友方方块阻挡</returns>
        private bool 判定视线畅通(Vector3I 炮塔位置, Vector3I 瞄准点位置, IMyCubeGrid 友方网格, Action<string> 输出 = null)
        {
            // 输出?.Invoke($"[视线判定] 炮塔: {炮塔位置.X},{炮塔位置.Y},{炮塔位置.Z}");
            // 输出?.Invoke($"[视线判定] 瞄准点: {瞄准点位置.X},{瞄准点位置.Y},{瞄准点位置.Z}");
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
            // int 循环次数 = 0;
            
            // 标记是否已经完全离开炮塔本体
            bool 已离开炮塔 = false;
            
            // DDA主循环 - 遍历射线经过的每个体素
            // 使用while循环，当射线穿出网格边界时自动停止
            while (true)
            {
                // 输出?.Invoke($"[缓存大小] 炮塔方块缓存数量={炮塔方块缓存.Count}");
                // 循环次数++;
                // 输出?.Invoke($"[视线判定循环] {循环次数}: \n 当前位置={当前位置.X},{当前位置.Y},{当前位置.Z}");
                // 检查当前位置（跳过起始位置）
                if (当前位置 != 炮塔位置)
                {
                    // 如果到达目标点，视线畅通
                    if (当前位置 == 瞄准点位置)
                        return true;

                    // 检查当前位置是否有友方方块（优先查缓存）
                    bool 存在方块;
                    if (已知方块位置缓存.Contains(当前位置))
                    {
                        // 缓存命中：存在方块
                        存在方块 = true;
                    }
                    else if (已知空位置缓存.Contains(当前位置))
                    {
                        // 缓存命中：不存在方块
                        存在方块 = false;
                    }
                    else
                    {
                        // 缓存未命中，调用API并缓存结果
                        存在方块 = 友方网格.CubeExists(当前位置);
                        if (存在方块)
                        {
                            已知方块位置缓存.Add(当前位置);
                        }
                        else
                        {
                            已知空位置缓存.Add(当前位置);
                        }
                    }

                    if (存在方块)
                    {
                        // 如果还没离开炮塔，检查是否仍在炮塔体内
                        if (!已离开炮塔)
                        {
                            // 判断当前方块是否是炮塔方块（查询缓存）
                            if (炮塔方块缓存.Contains(当前位置))
                            {
                                // 仍在炮塔体内，继续前进
                                // 输出?.Invoke($"[视线判定] 仍在炮塔体内，继续");
                            }
                            else
                            {
                                // 遇到非炮塔方块，说明已离开炮塔且被阻挡
                                // 输出?.Invoke($"[视线判定] 离开炮塔后遇到阻挡方块");
                                return false;
                            }
                        }
                        else
                        {
                            // 已经离开炮塔，再遇到方块就是被阻挡
                            // 输出?.Invoke($"[视线判定] 已离开炮塔，遇到阻挡方块");
                            return false;
                        }
                    }
                    else
                    {
                        // 当前位置没有方块，标记为已离开炮塔
                        已离开炮塔 = true;
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
                    // 输出?.Invoke($"[视线判定] 超出网格边界，视线畅通");
                    return true;
                }
            }
        }
    }
}
