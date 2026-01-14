using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// 射击控制器 - 负责炮塔的射击模式管理（齐射/轮射）
    /// 关键设计：ShootOnce是耗时操作，轮射时每帧只对一门炮塔调用
    /// </summary>
    public class 射击控制器
    {
        #region 字段

        /// <summary>参数管理器引用</summary>
        private 参数管理器 _参数;

        /// <summary>炮塔管理器引用</summary>
        private 炮塔管理器 _炮塔管理器;

        /// <summary>当前轮射索引（指向火炮类炮塔列表）</summary>
        private int _当前轮射索引;

        /// <summary>上次轮射帧</summary>
        private int _上次轮射帧;

        /// <summary>轮射间隔（帧）</summary>
        private int _轮射间隔;

        /// <summary>未能开火的炮塔索引集合（用于补射）</summary>
        private HashSet<int> _未开火炮塔;

        #endregion

        #region 属性

        /// <summary>当前轮射索引</summary>
        public int 当前轮射索引 { get { return _当前轮射索引; } }

        /// <summary>轮射间隔</summary>
        public int 轮射间隔 { get { return _轮射间隔; } }

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        public 射击控制器(参数管理器 参数, 炮塔管理器 炮塔管理器)
        {
            _参数 = 参数;
            _炮塔管理器 = 炮塔管理器;

            _当前轮射索引 = 0;
            _上次轮射帧 = -9999;
            _轮射间隔 = 60; // 默认1秒，会在初始化时更新
            _未开火炮塔 = new HashSet<int>();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 初始化射击控制器
        /// 计算轮射间隔等参数
        /// </summary>
        public void 初始化()
        {
            计算轮射间隔();
            _当前轮射索引 = 0;
            _未开火炮塔.Clear();
        }

        /// <summary>
        /// 执行射击控制
        /// 根据当前模式（齐射/轮射）控制炮塔开火
        /// </summary>
        /// <param name="当前帧">当前帧计数</param>
        /// <param name="瞄准点">当前瞄准点</param>
        /// <param name="存在有效目标">是否存在有效目标</param>
        public void 执行射击控制(int 当前帧, Vector3D 瞄准点, bool 存在有效目标)
        {
            if (!存在有效目标)
            {
                return;
            }

            // 非火炮类炮塔 - 持续瞄准但不主动开火（由AI块控制）
            // 或者使用齐射模式（每帧瞄准，让炮塔自己判断是否开火）
            执行非火炮类射击(当前帧, 瞄准点);

            // 火炮类炮塔 - 根据参数决定齐射或轮射
            if (_参数.火炮类使用轮射)
            {
                执行轮射(当前帧, 瞄准点);
            }
            else
            {
                执行火炮类齐射(当前帧, 瞄准点);
            }
        }

        /// <summary>
        /// 重置射击状态
        /// 目标丢失时调用
        /// </summary>
        public void 重置()
        {
            _当前轮射索引 = 0;
            _上次轮射帧 = -9999;
            _未开火炮塔.Clear();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 计算轮射间隔
        /// 基于火炮类炮塔的数量和装填时间
        /// 目标：在一个装填周期内，所有火炮依次开火一次
        /// </summary>
        private void 计算轮射间隔()
        {
            var 火炮列表 = _炮塔管理器.获取火炮类炮塔();
            if (火炮列表.Count == 0)
            {
                _轮射间隔 = 60; // 默认1秒
                return;
            }

            // 使用第一个火炮的装填时间作为基准
            // 装填时间（秒）* 60fps = 装填帧数
            // 轮射间隔 = 装填帧数 / 火炮数量
            double 装填时间秒 = 火炮列表[0].静态信息.装填时间;
            int 装填帧数 = (int)(装填时间秒 * 60);

            // 确保间隔至少为1帧，且不超过装填时间
            _轮射间隔 = Math.Max(1, 装填帧数 / Math.Max(1, 火炮列表.Count));
        }

        /// <summary>
        /// 执行非火炮类炮塔的射击
        /// 所有非火炮类炮塔持续瞄准目标
        /// 注意：不调用ShootOnce，让炮塔的AI自行控制开火
        /// </summary>
        private void 执行非火炮类射击(int 当前帧, Vector3D 瞄准点)
        {
            var 非火炮列表 = _炮塔管理器.获取非火炮类炮塔();

            for (int i = 0; i < 非火炮列表.Count; i++)
            {
                var 炮塔信息 = 非火炮列表[i];
                if (!炮塔信息.是否可用())
                    continue;

                var 炮塔 = 炮塔信息.炮塔方块;

                // 计算该炮塔的瞄准点（考虑平行偏差修正）
                Vector3D 实际瞄准点 = 瞄准点;
                if (!炮塔信息.是代表炮塔 && 炮塔信息.代表炮塔 != null)
                {
                    // 非代表炮塔，检查是否需要平行偏差修正
                    Vector3D 代表位置 = 炮塔信息.代表炮塔.获取位置();
                    Vector3D 当前位置 = 炮塔信息.获取位置();
                    double 距离平方 = (当前位置 - 代表位置).LengthSquared();

                    if (距离平方 > _参数.平行偏差修正阈值 * _参数.平行偏差修正阈值)
                    {
                        // 距离超过阈值，需要修正（简化版：直接使用代表炮塔的瞄准点）
                        // 完整版应该调用火控计算器的平行偏差修正
                    }
                }

                // 瞄准目标（不调用ShootOnce，让炮塔自行开火）
                炮塔控制器.瞄准(炮塔, 实际瞄准点);
            }
        }

        /// <summary>
        /// 执行火炮类炮塔的齐射
        /// 所有火炮类炮塔同时瞄准并开火
        /// 注意：齐射时仍然每帧只对一门炮塔调用ShootOnce以分摊性能开销
        /// </summary>
        private void 执行火炮类齐射(int 当前帧, Vector3D 瞄准点)
        {
            var 火炮列表 = _炮塔管理器.获取火炮类炮塔();
            if (火炮列表.Count == 0) return;

            // 齐射模式：所有炮塔持续瞄准，但ShootOnce按顺序轮流调用
            // 这样可以分摊性能开销，同时保持较高的射击频率
            int 本帧开火索引 = 当前帧 % 火炮列表.Count;

            for (int i = 0; i < 火炮列表.Count; i++)
            {
                var 炮塔信息 = 火炮列表[i];
                if (!炮塔信息.是否可用())
                    continue;

                var 炮塔 = 炮塔信息.炮塔方块;

                // 所有炮塔都瞄准
                bool 俯仰有效 = 炮塔控制器.瞄准带限制(
                    炮塔,
                    瞄准点,
                    炮塔信息.静态信息.俯仰下限,
                    炮塔信息.静态信息.俯仰上限);

                // 只有轮到的炮塔才开火（分摊ShootOnce开销）
                if (i == 本帧开火索引 && 俯仰有效)
                {
                    炮塔控制器.开火(炮塔);
                    炮塔信息.上次开火帧 = 当前帧;
                }
            }
        }

        /// <summary>
        /// 执行火炮类炮塔的轮射
        /// 按照轮射间隔依次开火，实现持续火力输出
        /// </summary>
        private void 执行轮射(int 当前帧, Vector3D 瞄准点)
        {
            var 火炮列表 = _炮塔管理器.获取火炮类炮塔();
            if (火炮列表.Count == 0) return;

            // 所有火炮都持续瞄准目标
            for (int i = 0; i < 火炮列表.Count; i++)
            {
                var 炮塔信息 = 火炮列表[i];
                if (!炮塔信息.是否可用())
                    continue;

                炮塔控制器.瞄准(炮塔信息.炮塔方块, 瞄准点);
            }

            // 检查是否到达轮射间隔
            if (当前帧 - _上次轮射帧 < _轮射间隔)
            {
                return;
            }

            // 确定当前应该开火的炮塔
            // 优先处理之前未能开火的炮塔（补射机制）
            HashSet<int> 开火序列 = new HashSet<int>(_未开火炮塔);
            开火序列.Add(_当前轮射索引);
            _未开火炮塔.Clear();

            // 对开火序列中的炮塔执行开火
            // 注意：每次轮射只调用一次ShootOnce
            bool 已开火 = false;
            foreach (int 索引 in 开火序列)
            {
                if (索引 < 0 || 索引 >= 火炮列表.Count)
                    continue;

                var 炮塔信息 = 火炮列表[索引];
                if (!炮塔信息.是否可用())
                {
                    _未开火炮塔.Add(索引);
                    continue;
                }

                var 炮塔 = 炮塔信息.炮塔方块;

                // 检查俯仰是否有效
                double 方位角, 俯仰角;
                炮塔控制器.计算瞄准角度(炮塔, 瞄准点, out 方位角, out 俯仰角);
                bool 俯仰有效 = 炮塔控制器.检查俯仰有效性(
                    俯仰角,
                    炮塔信息.静态信息.俯仰下限,
                    炮塔信息.静态信息.俯仰上限);

                if (俯仰有效 && !已开火)
                {
                    // 开火！每次轮射只调用一次ShootOnce
                    炮塔控制器.开火(炮塔);
                    炮塔信息.上次开火帧 = 当前帧;
                    已开火 = true;
                }
                else if (!俯仰有效)
                {
                    // 俯仰无效，加入未开火集合等待下次补射
                    _未开火炮塔.Add(索引);
                }
            }

            // 更新轮射索引和时间
            _当前轮射索引 = (_当前轮射索引 + 1) % Math.Max(1, 火炮列表.Count);
            _上次轮射帧 = 当前帧;
        }

        #endregion
    }
}
