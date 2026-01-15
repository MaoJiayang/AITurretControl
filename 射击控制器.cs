using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// 火炮类型枚举 - 按射速分组
    /// 不同射速的火炮需要分开轮射
    /// </summary>
    public enum 火炮类型分组
    {
        火炮炮塔,       // 12秒装填，弹匣2发
        突击炮炮塔,     // 6秒装填，弹匣2发
        小型突击炮炮塔  // 6秒装填，弹匣1发
    }

    /// <summary>
    /// 轮射组 - 管理同类型火炮的轮射状态
    /// </summary>
    public class 轮射组
    {
        /// <summary>组内炮塔列表</summary>
        public List<炮塔运行时信息> 炮塔列表;

        /// <summary>当前轮射索引</summary>
        public int 当前索引;

        /// <summary>上次轮射帧</summary>
        public int 上次轮射帧;

        /// <summary>轮射间隔（帧）</summary>
        public int 轮射间隔;

        /// <summary>每次轮射开火数量（射击间隔小于1帧时合并射击）</summary>
        public int 每次开火数量;

        /// <summary>未开火炮塔索引集合</summary>
        public HashSet<int> 未开火炮塔;

        /// <summary>弹匣容量</summary>
        public int 弹匣容量;

        /// <summary>装填帧数</summary>
        public int 装填帧数;

        public 轮射组()
        {
            炮塔列表 = new List<炮塔运行时信息>();
            当前索引 = 0;
            上次轮射帧 = -9999;
            轮射间隔 = 60;
            每次开火数量 = 1;
            未开火炮塔 = new HashSet<int>();
            弹匣容量 = 2;
            装填帧数 = 720;
        }

        /// <summary>
        /// 计算轮射参数
        /// </summary>
        public void 计算轮射参数()
        {
            if (炮塔列表.Count == 0)
            {
                轮射间隔 = 60;
                每次开火数量 = 1;
                return;
            }

            // 获取第一个炮塔的信息作为基准
            var 首炮信息 = 炮塔列表[0].静态信息;
            弹匣容量 = 首炮信息.弹匣容量;
            装填帧数 = (int)(首炮信息.装填时间 * 60);

            // 一个装填周期内的总发弹数 = 弹匣容量 * 炮塔数量
            int 总发弹数 = 弹匣容量 * 炮塔列表.Count;

            // 理论轮射间隔 = 装填帧数 / 总发弹数
            double 理论间隔 = (double)装填帧数 / 总发弹数;

            if (理论间隔 >= 1.0)
            {
                // 间隔足够，每次开火一门
                轮射间隔 = (int)Math.Ceiling(理论间隔);
                每次开火数量 = 1;
            }
            else
            {
                // 间隔太小，需要合并射击
                // 计算每帧需要开火多少门炮
                每次开火数量 = (int)Math.Ceiling(1.0 / 理论间隔);
                每次开火数量 = Math.Min(每次开火数量, 炮塔列表.Count);
                轮射间隔 = 1;
            }
        }
    }

    /// <summary>
    /// 射击控制器 - 负责炮塔的射击模式管理（齐射/轮射）
    /// 关键设计：
    /// 1. 火炮按类型分组轮射（火炮、突击炮、小型突击炮）
    /// 2. ShootOnce是耗时操作，每帧调用次数应尽量少
    /// 3. 非火炮类使用Shoot属性持续射击
    /// </summary>
    public class 射击控制器
    {
        #region 字段

        /// <summary>参数管理器引用</summary>
        private 参数管理器 _参数;

        /// <summary>炮塔管理器引用</summary>
        private 炮塔管理器 _炮塔管理器;

        /// <summary>火炮轮射组（按SubtypeId分组）</summary>
        private Dictionary<string, 轮射组> _轮射组字典;

        /// <summary>轮射组执行顺序（轮流执行各组的ShootOnce）</summary>
        private List<string> _轮射组顺序;

        /// <summary>当前执行的轮射组索引</summary>
        private int _当前组索引;

        /// <summary>非火炮类是否已开启射击</summary>
        private bool _非火炮已开火;

        #endregion

        #region 属性

        /// <summary>当前轮射组名称</summary>
        public string 当前轮射组名称
        {
            get
            {
                if (_轮射组顺序 == null || _轮射组顺序.Count == 0)
                    return "无";
                return _轮射组顺序[_当前组索引 % _轮射组顺序.Count];
            }
        }

        /// <summary>轮射组总数</summary>
        public int 轮射组数量
        {
            get { return _轮射组字典?.Count ?? 0; }
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        public 射击控制器(参数管理器 参数, 炮塔管理器 炮塔管理器)
        {
            _参数 = 参数;
            _炮塔管理器 = 炮塔管理器;

            _轮射组字典 = new Dictionary<string, 轮射组>();
            _轮射组顺序 = new List<string>();
            _当前组索引 = 0;
            _非火炮已开火 = false;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 初始化射击控制器
        /// 按炮塔类型创建轮射组
        /// </summary>
        public void 初始化()
        {
            _轮射组字典.Clear();
            _轮射组顺序.Clear();
            _当前组索引 = 0;
            _非火炮已开火 = false;

            // 将火炮按SubtypeId分组
            var 火炮列表 = _炮塔管理器.获取火炮类炮塔();
            for (int i = 0; i < 火炮列表.Count; i++)
            {
                var 炮塔 = 火炮列表[i];
                string 类型键 = 炮塔.炮塔方块.BlockDefinition.SubtypeId;

                轮射组 组;
                if (!_轮射组字典.TryGetValue(类型键, out 组))
                {
                    组 = new 轮射组();
                    _轮射组字典[类型键] = 组;
                    _轮射组顺序.Add(类型键);
                }
                组.炮塔列表.Add(炮塔);
            }

            // 计算每个组的轮射参数
            foreach (var 组 in _轮射组字典.Values)
            {
                组.计算轮射参数();
            }
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
            if (!存在有效目标 && _非火炮已开火)
            {
                停止非火炮类射击();
                return;
            }
            // 非火炮类炮塔 - 持续瞄准并开启射击（使用Shoot属性）
            执行非火炮类射击(当前帧, 瞄准点);

            // 火炮类炮塔 - 根据参数决定齐射或轮射
            if (_参数.火炮类使用轮射)
            {
                执行分组轮射(当前帧, 瞄准点);
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
            // 重置所有轮射组
            foreach (var 组 in _轮射组字典.Values)
            {
                组.当前索引 = 0;
                组.上次轮射帧 = -9999;
                组.未开火炮塔.Clear();
            }
            _当前组索引 = 0;

            // 停止非火炮射击
            停止非火炮类射击();
        }

        #endregion

        #region 私有方法 - 非火炮类

        /// <summary>
        /// 执行非火炮类炮塔的射击
        /// 所有非火炮类炮塔持续瞄准目标并开启射击
        /// 炮塔类实现了IMyUserControllableGun，需要设置Shoot=true
        /// </summary>
        private void 执行非火炮类射击(int 当前帧, Vector3D 瞄准点)
        {
            var 非火炮列表 = _炮塔管理器.获取非火炮类炮塔();
            if (非火炮列表.Count == 0) return;

            for (int i = 0; i < 非火炮列表.Count; i++)
            {
                var 炮塔信息 = 非火炮列表[i];
                if (!炮塔信息.是否可用())
                    continue;

                var 炮塔 = 炮塔信息.炮塔方块;

                // 瞄准目标
                bool 俯仰有效 = 炮塔控制器.瞄准带限制(
                    炮塔,
                    瞄准点,
                    炮塔信息.静态信息.俯仰下限,
                    炮塔信息.静态信息.俯仰上限);

                // 俯仰有效时开启射击
                if (俯仰有效)
                {
                    // 使用Shoot属性持续射击，而不是ShootOnce
                    // 炮塔类实现IMyUserControllableGun，需要主动开启射击
                    炮塔.Shoot = true;
                }
                else
                {
                    炮塔.Shoot = false;
                }
            }

            _非火炮已开火 = true;
        }

        /// <summary>
        /// 停止非火炮类的射击
        /// </summary>
        private void 停止非火炮类射击()
        {
            var 非火炮列表 = _炮塔管理器.获取非火炮类炮塔();
            for (int i = 0; i < 非火炮列表.Count; i++)
            {
                var 炮塔信息 = 非火炮列表[i];
                if (炮塔信息.炮塔方块 != null)
                {
                    炮塔信息.炮塔方块.Shoot = false;
                }
            }
            _非火炮已开火 = false;
        }

        #endregion

        #region 私有方法 - 火炮类齐射

        /// <summary>
        /// 执行火炮类炮塔的齐射
        /// 所有火炮类炮塔同时瞄准，轮流调用ShootOnce
        /// </summary>
        private void 执行火炮类齐射(int 当前帧, Vector3D 瞄准点)
        {
            var 火炮列表 = _炮塔管理器.获取火炮类炮塔();
            if (火炮列表.Count == 0) return;

            // 齐射模式：所有炮塔持续瞄准
            for (int i = 0; i < 火炮列表.Count; i++)
            {
                var 炮塔信息 = 火炮列表[i];
                if (!炮塔信息.是否可用())
                    continue;

                炮塔控制器.瞄准(炮塔信息.炮塔方块, 瞄准点);
            }

            // 轮流执行ShootOnce，每帧只对一组中的一门炮调用
            // 避免性能问题
            if (_轮射组顺序.Count == 0) return;

            string 当前组键 = _轮射组顺序[_当前组索引 % _轮射组顺序.Count];
            轮射组 当前组;
            if (!_轮射组字典.TryGetValue(当前组键, out 当前组)) return;

            // 对当前组的一门炮执行ShootOnce
            int 炮塔索引 = 当前帧 % Math.Max(1, 当前组.炮塔列表.Count);
            if (炮塔索引 < 当前组.炮塔列表.Count)
            {
                var 炮塔信息 = 当前组.炮塔列表[炮塔索引];
                if (炮塔信息.是否可用())
                {
                    double 方位角, 俯仰角;
                    炮塔控制器.计算瞄准角度(炮塔信息.炮塔方块, 瞄准点, out 方位角, out 俯仰角);
                    bool 俯仰有效 = 炮塔控制器.检查俯仰有效性(
                        俯仰角,
                        炮塔信息.静态信息.俯仰下限,
                        炮塔信息.静态信息.俯仰上限);

                    if (俯仰有效)
                    {
                        炮塔控制器.开火(炮塔信息.炮塔方块);
                        炮塔信息.上次开火帧 = 当前帧;
                    }
                }
            }

            // 轮换到下一组
            _当前组索引 = (_当前组索引 + 1) % Math.Max(1, _轮射组顺序.Count);
        }

        #endregion

        #region 私有方法 - 火炮类分组轮射

        /// <summary>
        /// 执行分组轮射
        /// 各类型火炮独立轮射，避免射速不一致导致的问题
        /// </summary>
        private void 执行分组轮射(int 当前帧, Vector3D 瞄准点)
        {
            // 所有火炮都持续瞄准目标
            var 火炮列表 = _炮塔管理器.获取火炮类炮塔();
            for (int i = 0; i < 火炮列表.Count; i++)
            {
                var 炮塔信息 = 火炮列表[i];
                if (!炮塔信息.是否可用())
                    continue;

                炮塔控制器.瞄准(炮塔信息.炮塔方块, 瞄准点);
            }

            // 对每个轮射组独立执行轮射
            // 每帧最多执行一次ShootOnce（按组轮流）
            if (_轮射组顺序.Count == 0) return;

            // 获取当前应该执行ShootOnce的组
            string 当前组键 = _轮射组顺序[_当前组索引 % _轮射组顺序.Count];
            轮射组 当前组;
            if (!_轮射组字典.TryGetValue(当前组键, out 当前组)) return;

            // 检查当前组是否到达轮射间隔
            if (当前帧 - 当前组.上次轮射帧 >= 当前组.轮射间隔)
            {
                执行单组轮射(当前帧, 瞄准点, 当前组);
            }

            // 轮换到下一组
            _当前组索引 = (_当前组索引 + 1) % Math.Max(1, _轮射组顺序.Count);
        }

        /// <summary>
        /// 执行单个轮射组的轮射
        /// </summary>
        private void 执行单组轮射(int 当前帧, Vector3D 瞄准点, 轮射组 组)
        {
            if (组.炮塔列表.Count == 0) return;

            // 收集需要开火的炮塔索引
            HashSet<int> 开火序列 = new HashSet<int>(组.未开火炮塔);

            // 添加当前应该开火的炮塔
            for (int i = 0; i < 组.每次开火数量; i++)
            {
                int 索引 = (组.当前索引 + i) % 组.炮塔列表.Count;
                开火序列.Add(索引);
            }
            组.未开火炮塔.Clear();

            // 执行开火（每帧最多调用一次ShootOnce）
            bool 已开火 = false;
            foreach (int 索引 in 开火序列)
            {
                if (索引 < 0 || 索引 >= 组.炮塔列表.Count)
                    continue;

                var 炮塔信息 = 组.炮塔列表[索引];
                if (!炮塔信息.是否可用())
                {
                    组.未开火炮塔.Add(索引);
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

                if (俯仰有效)
                {
                    if (!已开火)
                    {
                        // 每帧最多执行一次ShootOnce
                        炮塔控制器.开火(炮塔);
                        炮塔信息.上次开火帧 = 当前帧;
                        已开火 = true;
                    }
                    else
                    {
                        // 本帧已经开火过，将这门炮加入未开火列表下帧补射
                        组.未开火炮塔.Add(索引);
                    }
                }
                else
                {
                    // 俯仰无效，加入未开火集合等待下次补射
                    组.未开火炮塔.Add(索引);
                }
            }

            // 更新轮射索引和时间
            组.当前索引 = (组.当前索引 + 组.每次开火数量) % Math.Max(1, 组.炮塔列表.Count);
            组.上次轮射帧 = 当前帧;
        }

        #endregion
    }
}
