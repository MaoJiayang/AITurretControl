using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// 炮塔聚类组 - 管理同一弹药类型且位置相近的炮塔
    /// 同一聚类组内的炮塔共享火控计算结果
    /// </summary>
    public class 炮塔聚类组
    {
        /// <summary>分组键（弹药类型）</summary>
        public string 分组键 { get; private set; }

        /// <summary>代表炮塔（用于火控计算）</summary>
        public 炮塔运行时信息 代表炮塔 { get; private set; }

        /// <summary>组内所有炮塔列表</summary>
        public List<炮塔运行时信息> 炮塔列表 { get; private set; }

        /// <summary>缓存的瞄准点（世界坐标）</summary>
        public Vector3D 缓存瞄准点 { get; set; }

        /// <summary>缓存的俯仰角（弧度）</summary>
        public double 缓存俯仰角 { get; set; }

        /// <summary>缓存的方位角（弧度）</summary>
        public double 缓存方位角 { get; set; }

        /// <summary>缓存是否有效</summary>
        public bool 缓存有效 { get; set; }

        /// <summary>上次火控计算帧</summary>
        public int 上次计算帧 { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public 炮塔聚类组(string 分组键, 炮塔运行时信息 代表)
        {
            this.分组键 = 分组键;
            this.代表炮塔 = 代表;
            this.炮塔列表 = new List<炮塔运行时信息>();
            this.缓存瞄准点 = Vector3D.Zero;
            this.缓存俯仰角 = 0;
            this.缓存方位角 = 0;
            this.缓存有效 = false;
            this.上次计算帧 = -9999;

            代表.是代表炮塔 = true;
            代表.代表炮塔 = null;
            炮塔列表.Add(代表);
        }

        /// <summary>
        /// 添加炮塔到聚类组
        /// </summary>
        public void 添加炮塔(炮塔运行时信息 炮塔)
        {
            炮塔.是代表炮塔 = false;
            炮塔.代表炮塔 = 代表炮塔;
            炮塔列表.Add(炮塔);
        }

        /// <summary>
        /// 获取组内可用炮塔数量
        /// </summary>
        public int 获取可用数量()
        {
            int 数量 = 0;
            for (int i = 0; i < 炮塔列表.Count; i++)
            {
                if (炮塔列表[i].是否可用())
                    数量++;
            }
            return 数量;
        }

        /// <summary>
        /// 获取组内火炮类炮塔数量（用于轮射计算）
        /// </summary>
        public int 获取火炮类数量()
        {
            int 数量 = 0;
            for (int i = 0; i < 炮塔列表.Count; i++)
            {
                if (炮塔列表[i].是否可用() && 炮塔列表[i].静态信息.是火炮类)
                    数量++;
            }
            return 数量;
        }

        /// <summary>
        /// 使缓存失效
        /// </summary>
        public void 使缓存失效()
        {
            缓存有效 = false;
        }
    }

    /// <summary>
    /// 炮塔管理器 - 负责炮塔的识别、分组和聚类管理
    /// </summary>
    public partial class 炮塔管理器
    {
        #region 字段

        /// <summary>网格终端系统引用</summary>
        private IMyGridTerminalSystem _网格终端;

        /// <summary>参数管理器引用</summary>
        private 参数管理器 _参数;

        /// <summary>所有炮塔的运行时信息列表</summary>
        private List<炮塔运行时信息> _所有炮塔;

        /// <summary>按分组键组织的聚类组字典</summary>
        private Dictionary<string, List<炮塔聚类组>> _聚类组字典;

        /// <summary>火炮类炮塔列表（用于轮射控制）</summary>
        private List<炮塔运行时信息> _火炮类炮塔;

        /// <summary>非火炮类炮塔列表（齐射控制）</summary>
        private List<炮塔运行时信息> _非火炮类炮塔;

        /// <summary>是否已初始化</summary>
        private bool _已初始化;

        /// <summary>上次更新炮塔列表的帧计数</summary>
        private int _上次更新帧;

        /// <summary>调试输出委托</summary>
        private Action<string> _输出;

        #endregion

        #region 属性

        /// <summary>所有炮塔数量</summary>
        public int 炮塔总数 => _所有炮塔?.Count ?? 0;

        /// <summary>火炮类炮塔数量</summary>
        public int 火炮类数量 => _火炮类炮塔?.Count ?? 0;

        /// <summary>聚类组总数</summary>
        public int 聚类组总数
        {
            get
            {
                int 总数 = 0;
                if (_聚类组字典 != null)
                {
                    foreach (var 组列表 in _聚类组字典.Values)
                    {
                        总数 += 组列表.Count;
                    }
                }
                return 总数;
            }
        }

        /// <summary>是否已完成初始化</summary>
        public bool 已初始化 => _已初始化;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="网格终端">网格终端系统</param>
        /// <param name="参数">参数管理器</param>
        /// <param name="输出">调试输出委托（可选）</param>
        public 炮塔管理器(IMyGridTerminalSystem 网格终端, 参数管理器 参数, Action<string> 输出 = null)
        {
            _网格终端 = 网格终端;
            _参数 = 参数;
            _输出 = 输出;

            _所有炮塔 = new List<炮塔运行时信息>();
            _聚类组字典 = new Dictionary<string, List<炮塔聚类组>>();
            _火炮类炮塔 = new List<炮塔运行时信息>();
            _非火炮类炮塔 = new List<炮塔运行时信息>();

            _已初始化 = false;
            _上次更新帧 = -9999;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 初始化或刷新炮塔列表
        /// 扫描网格上的所有炮塔并进行分组和聚类
        /// </summary>
        /// <param name="当前帧">当前帧计数</param>
        /// <returns>是否成功初始化</returns>
        public bool 刷新炮塔列表(int 当前帧)
        {
            _上次更新帧 = 当前帧;

            // 清空现有数据
            _所有炮塔.Clear();
            _聚类组字典.Clear();
            _火炮类炮塔.Clear();
            _非火炮类炮塔.Clear();

            // 获取所有炮塔方块
            List<IMyLargeTurretBase> 炮塔方块列表 = new List<IMyLargeTurretBase>();
            _网格终端.GetBlocksOfType(炮塔方块列表, 炮塔 => 炮塔.IsFunctional);

            if (炮塔方块列表.Count == 0)
            {
                _输出?.Invoke("未找到任何可用炮塔");
                _已初始化 = false;
                return false;
            }

            // 创建运行时信息并分类
            for (int i = 0; i < 炮塔方块列表.Count; i++)
            {
                var 运行时信息 = new 炮塔运行时信息(炮塔方块列表[i]);
                
                // 按是否为火炮类分类
                if (运行时信息.静态信息.是火炮类)
                {
                    // 火炮类炮塔，始终添加
                    _所有炮塔.Add(运行时信息);
                    _火炮类炮塔.Add(运行时信息);
                }
                else
                {
                    // 非火炮类炮塔，根据参数决定是否添加
                    if (_参数.托管机枪类)
                    {
                        _所有炮塔.Add(运行时信息);
                        _非火炮类炮塔.Add(运行时信息);
                    }
                }
            }

            // 执行聚类分组
            执行聚类分组();

            _已初始化 = true;
            _输出?.Invoke($"炮塔管理器初始化完成: {炮塔总数}个炮塔, {聚类组总数}个聚类组");

            return true;
        }

        /// <summary>
        /// 获取所有聚类组的迭代器
        /// 用于火控计算遍历
        /// </summary>
        public IEnumerable<炮塔聚类组> 获取所有聚类组()
        {
            foreach (var 组列表 in _聚类组字典.Values)
            {
                for (int i = 0; i < 组列表.Count; i++)
                {
                    yield return 组列表[i];
                }
            }
        }

        /// <summary>
        /// 获取指定弹药类型的聚类组列表
        /// </summary>
        /// <param name="分组键">弹药类型的分组键</param>
        /// <returns>聚类组列表，如果不存在返回空列表</returns>
        public List<炮塔聚类组> 获取聚类组(string 分组键)
        {
            List<炮塔聚类组> 结果;
            if (_聚类组字典.TryGetValue(分组键, out 结果))
            {
                return 结果;
            }
            return new List<炮塔聚类组>();
        }

        /// <summary>
        /// 获取所有火炮类炮塔（用于轮射控制）
        /// </summary>
        public List<炮塔运行时信息> 获取火炮类炮塔()
        {
            return _火炮类炮塔;
        }

        /// <summary>
        /// 获取所有非火炮类炮塔（用于齐射控制）
        /// </summary>
        public List<炮塔运行时信息> 获取非火炮类炮塔()
        {
            return _非火炮类炮塔;
        }

        /// <summary>
        /// 使所有聚类组的缓存失效
        /// 当目标改变时调用
        /// </summary>
        public void 使所有缓存失效()
        {
            foreach (var 组列表 in _聚类组字典.Values)
            {
                for (int i = 0; i < 组列表.Count; i++)
                {
                    组列表[i].使缓存失效();
                }
            }
        }

        /// <summary>
        /// 检查是否需要刷新炮塔列表
        /// </summary>
        /// <param name="当前帧">当前帧计数</param>
        /// <returns>是否需要刷新</returns>
        public bool 需要刷新(int 当前帧)
        {
            return !_已初始化 || (当前帧 - _上次更新帧 >= _参数.方块更新间隔);
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 执行聚类分组
        /// 将同类型且位置相近的炮塔归入同一聚类组
        /// </summary>
        private void 执行聚类分组()
        {
            double 聚类距离平方 = _参数.聚类距离 * _参数.聚类距离;

            // 按分组键（弹药类型）分类
            Dictionary<string, List<炮塔运行时信息>> 按类型分组 = new Dictionary<string, List<炮塔运行时信息>>();

            for (int i = 0; i < _所有炮塔.Count; i++)
            {
                var 炮塔 = _所有炮塔[i];
                string 键 = 炮塔.分组键;

                List<炮塔运行时信息> 列表;
                if (!按类型分组.TryGetValue(键, out 列表))
                {
                    列表 = new List<炮塔运行时信息>();
                    按类型分组[键] = 列表;
                }
                列表.Add(炮塔);
            }

            // 对每种类型进行空间聚类
            foreach (var kvp in 按类型分组)
            {
                string 分组键 = kvp.Key;
                List<炮塔运行时信息> 同类炮塔 = kvp.Value;
                List<炮塔聚类组> 聚类组列表 = new List<炮塔聚类组>();

                // 标记哪些炮塔已被分配
                bool[] 已分配 = new bool[同类炮塔.Count];

                for (int i = 0; i < 同类炮塔.Count; i++)
                {
                    if (已分配[i]) continue;

                    // 创建新的聚类组，当前炮塔为代表
                    var 新聚类组 = new 炮塔聚类组(分组键, 同类炮塔[i]);
                    已分配[i] = true;

                    Vector3D 代表位置 = 同类炮塔[i].获取位置();

                    // 查找附近的同类炮塔加入该聚类组
                    for (int j = i + 1; j < 同类炮塔.Count; j++)
                    {
                        if (已分配[j]) continue;

                        Vector3D 当前位置 = 同类炮塔[j].获取位置();
                        double 距离平方 = (当前位置 - 代表位置).LengthSquared();

                        if (距离平方 <= 聚类距离平方)
                        {
                            新聚类组.添加炮塔(同类炮塔[j]);
                            已分配[j] = true;
                        }
                    }

                    聚类组列表.Add(新聚类组);
                }

                _聚类组字典[分组键] = 聚类组列表;
            }
        }

        #endregion
    }
}
