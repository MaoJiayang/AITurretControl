using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// 射击控制器 - 负责炮塔的射击开火管理
    /// 关键设计：
    /// 1. 维护"已开火"和"未开火"集合，避免重复设置Shoot属性
    /// 2. 对每个聚类组应用统一的火控参数
    /// 3. 非代表炮塔应用平行偏差修正
    /// </summary>
    public class 射击控制器
    {
        #region 字段

        /// <summary>参数管理器引用</summary>
        private 参数管理器 _参数;

        /// <summary>炮塔管理器引用</summary>
        private 炮塔管理器 _炮塔管理器;

        /// <summary>火控计算器引用</summary>
        private 火控计算器 _火控计算器;

        /// <summary>已开火炮塔集合（Shoot已设为true的炮塔）</summary>
        private HashSet<IMyLargeTurretBase> _已开火集合;

        /// <summary>未开火炮塔集合（目标不可达的炮塔）</summary>
        private HashSet<IMyLargeTurretBase> _未开火集合;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        public 射击控制器(参数管理器 参数, 炮塔管理器 炮塔管理器, 火控计算器 火控计算器)
        {
            _参数 = 参数;
            _炮塔管理器 = 炮塔管理器;
            _火控计算器 = 火控计算器;

            _已开火集合 = new HashSet<IMyLargeTurretBase>();
            _未开火集合 = new HashSet<IMyLargeTurretBase>();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 初始化射击控制器
        /// </summary>
        public void 初始化()
        {
            _已开火集合.Clear();
            _未开火集合.Clear();
        }

        /// <summary>
        /// 对聚类组开启射击
        /// 为组内所有炮塔计算瞄准点并控制开火
        /// </summary>
        /// <param name="聚类组">炮塔聚类组</param>
        /// <param name="舰船速度">本舰速度向量</param>
        /// <param name="重力向量">重力加速度向量</param>
        /// <param name="时间偏移ms">时间偏移（毫秒）</param>
        public void 对聚类组开启射击(
            炮塔聚类组 聚类组,
            Vector3D 舰船速度,
            Vector3D 重力向量,
            long 时间偏移ms)
        {
            if (聚类组 == null || 聚类组.炮塔列表.Count == 0)
                return;

            var 代表 = 聚类组.代表炮塔;
            if (!代表.是否可用())
                return;

            // 获取代表炮塔的火控参数
            Vector3D 代表位置 = 代表.获取位置();
            double 弹速 = 代表.静态信息.弹速;
            bool 受重力影响 = 代表.静态信息.受重力影响;

            // 计算代表炮塔的瞄准点
            Vector3D 代表瞄准点 = _火控计算器.计算瞄准点(
                代表位置,
                弹速,
                受重力影响,
                舰船速度,
                重力向量,
                时间偏移ms);

            // 缓存到聚类组
            聚类组.缓存瞄准点 = 代表瞄准点;
            聚类组.缓存有效 = true;

            // 获取目标速度（用于平行偏差修正，传入时间偏移参数）
            Vector3D 目标速度 = _火控计算器.获取当前目标速度(时间偏移ms);

            // 对组内每个炮塔执行开火控制
            for (int i = 0; i < 聚类组.炮塔列表.Count; i++)
            {
                var 炮塔信息 = 聚类组.炮塔列表[i];
                对单个炮塔开启射击(炮塔信息, 代表瞄准点, 代表位置, 弹速, 舰船速度, 目标速度);
            }
        }

        /// <summary>
        /// 停止所有射击
        /// 目标丢失时调用
        /// </summary>
        public void 停止所有射击()
        {
            // 对所有已开火的炮塔关闭射击
            foreach (var 炮塔 in _已开火集合)
            {
                if (炮塔 != null)
                {
                    炮塔.Shoot = false;
                }
            }

            _已开火集合.Clear();
            _未开火集合.Clear();
        }

        /// <summary>
        /// 重置射击状态
        /// </summary>
        public void 重置()
        {
            停止所有射击();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 对单个炮塔开启射击
        /// </summary>
        /// <param name="炮塔信息">炮塔运行时信息</param>
        /// <param name="代表瞄准点">代表炮塔计算的瞄准点</param>
        /// <param name="代表位置">代表炮塔位置</param>
        /// <param name="弹速">弹速</param>
        /// <param name="舰船速度">舰船速度</param>
        /// <param name="目标速度">目标速度</param>
        private void 对单个炮塔开启射击(
            炮塔运行时信息 炮塔信息,
            Vector3D 代表瞄准点,
            Vector3D 代表位置,
            double 弹速,
            Vector3D 舰船速度,
            Vector3D 目标速度)
        {
            if (!炮塔信息.是否可用())
                return;

            var 炮塔 = 炮塔信息.炮塔方块;
            Vector3D 当前位置 = 炮塔信息.获取位置();

            // 计算该炮塔的实际瞄准点
            Vector3D 瞄准点;
            if (炮塔信息.是代表炮塔)
            {
                // 代表炮塔直接使用计算结果
                瞄准点 = 代表瞄准点;
            }
            else
            {
                // 非代表炮塔应用平行偏差修正
                瞄准点 = _火控计算器.计算平行偏差修正(
                    代表瞄准点,
                    代表位置,
                    当前位置,
                    弹速,
                    舰船速度,
                    目标速度);
            }

            // 检查目标可达性（瞄准方法内部会检查射程和俯仰）
            bool 目标可达 = 炮塔控制器.瞄准(炮塔信息, 瞄准点);

            if (目标可达)
            {
                // 目标可达
                if (!_已开火集合.Contains(炮塔))
                {
                    // 尚未开火，设置Shoot=true并加入已开火集合
                    炮塔.Shoot = true;
                    _已开火集合.Add(炮塔);
                }
                // 已在已开火集合中，不重复设置

                // 从未开火集合中移除（如果存在）
                _未开火集合.Remove(炮塔);
            }
            else
            {
                // 目标不可达
                if (_已开火集合.Contains(炮塔))
                {
                    // 之前已开火，现在不可达，需要关闭射击
                    炮塔.Shoot = false;
                    _已开火集合.Remove(炮塔);
                }

                // 加入未开火集合
                _未开火集合.Add(炮塔);
            }
        }

        #endregion
    }
}
