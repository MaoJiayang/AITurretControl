using System;
using VRage.Game.ModAPI.Ingame;
using Sandbox.ModAPI.Ingame;
using VRageMath;
using System.Text;
using System.Collections.Generic;

namespace IngameScript
{
    /// <summary>
    /// 火控系统参数管理器 - 统一管理所有超参数
    /// </summary>
    public class 参数管理器
    {
        public string 版本号 { get; } = "1.0.0-AI火控";

        #region 火控系统参数

        /// <summary>
        /// 主驾驶舱标签
        /// 用于识别主控制器，找不到时使用任意驾驶舱
        /// </summary>
        public string 主驾驶舱标签 { get; set; } = "主";

        /// <summary>
        /// 聚类距离（米）
        /// 同类炮塔在此距离内共享火控计算结果
        /// </summary>
        public double 聚类距离 { get; set; } = 25.0;

        /// <summary>
        /// 火控更新跳帧数
        /// 每隔多少帧执行一次火控计算
        /// </summary>
        public int 火控更新跳帧 { get; set; } = 6;

        /// <summary>
        /// 弹道计算迭代次数
        /// </summary>
        public int 弹道迭代次数 { get; set; } = 3;

        /// <summary>
        /// 平行偏差修正阈值（米）
        /// 炮塔与代表炮塔距离超过此值时进行平行偏差修正
        /// </summary>
        public double 平行偏差修正阈值 { get; set; } = 5.0;

        #endregion

        #region 制导相关参数
        /// <summary>
        /// 向量最小有效长度
        /// </summary>
        public double 最小向量长度 { get; set; } = 1e-6;

        /// <summary>
        /// 时间常数(秒) - 用于时间戳计算
        /// </summary>
        public double 时间常数 { get; set; } = 1f / 60f;

        #endregion

        #region 目标跟踪器参数

        /// <summary>
        /// 目标历史记录最大长度
        /// </summary>
        public int 目标历史最大长度 { get; set; } = 12;

        #endregion

        #region 方块更新参数

        /// <summary>
        /// 重新初始化方块的间隔(帧数)
        /// </summary>
        public int 方块更新间隔 { get; set; } = 3600;

        #endregion

        #region 性能统计参数

        /// <summary>
        /// 性能统计重置间隔(帧数)
        /// </summary>
        public int 性能统计重置间隔 { get; set; } = 600;

        #endregion

        #region 委托注册系统
        /// <summary>
        /// 参数注册表 - 存储所有参数的访问委托
        /// </summary>
        private Dictionary<string, 参数描述符> 参数注册表;

        /// <summary>
        /// 注册所有参数到注册表中
        /// 添加新参数在此方法中添加注册代码
        /// 并添加相关解析和格式化方法
        /// </summary>
        private void 注册所有参数()
        {
            参数注册表 = new Dictionary<string, 参数描述符>();

            // ============ 火控系统参数 ============
            注册参数("主驾驶舱标签",
                () => 主驾驶舱标签,
                v => 主驾驶舱标签 = v,
                "主驾驶舱识别标签，找不到时使用任意驾驶舱");

            注册参数("聚类距离",
                () => 聚类距离.ToString(),
                v => { double val; if (double.TryParse(v, out val)) 聚类距离 = val; },
                "同类炮塔聚类距离(米)，此范围内共享火控计算");

            注册参数("火控更新跳帧",
                () => 火控更新跳帧.ToString(),
                v => { int val; if (int.TryParse(v, out val)) 火控更新跳帧 = val; },
                "火控计算更新间隔(帧数)");

            注册参数("弹道迭代次数",
                () => 弹道迭代次数.ToString(),
                v => { int val; if (int.TryParse(v, out val)) 弹道迭代次数 = val; },
                "弹道计算迭代次数");

            注册参数("平行偏差修正阈值",
                () => 平行偏差修正阈值.ToString(),
                v => { double val; if (double.TryParse(v, out val)) 平行偏差修正阈值 = val; },
                "触发平行偏差修正的距离阈值(米)");

            // ============ 目标跟踪参数 ============
            注册参数("目标历史最大长度",
                () => 目标历史最大长度.ToString(),
                v => { int val; if (int.TryParse(v, out val)) 目标历史最大长度 = val; },
                "目标历史记录最大长度");

            // ============ 系统参数 ============
            注册参数("方块更新间隔",
                () => 方块更新间隔.ToString(),
                v => { int val; if (int.TryParse(v, out val)) 方块更新间隔 = val; },
                "重新扫描方块的间隔(帧数)");

            注册参数("性能统计重置间隔",
                () => 性能统计重置间隔.ToString(),
                v => { int val; if (int.TryParse(v, out val)) 性能统计重置间隔 = val; },
                "性能统计重置间隔(帧数)");
        }

        #endregion

        #region 参数获取方法

        /// <summary>
        /// 获取计算后的时间常数
        /// </summary>
        public double 获取时间常数()
        {
            return 时间常数;
        }

        #endregion

        #region 参数辅助方法
        
        /// <summary>
        /// 从字符串解析字符串（处理空值和trim）
        /// </summary>
        private string 解析字符串(string 值字符串)
        {
            if (string.IsNullOrWhiteSpace(值字符串))
                return "";

            return 值字符串.Trim();
        }
        
        #endregion
        
// ---------- 基本上，不需要改动以下代码 ----------
        /// <summary>
        /// 注册单个参数到注册表
        /// </summary>
        private void 注册参数(string 参数名, Func<string> 获取值, Action<string> 设置值, string 描述 = "", bool 空值时隐藏 = false)
        {
            参数注册表[参数名] = new 参数描述符(获取值, 设置值, 描述, 空值时隐藏);
        }

        #region 构造函数

        /// <summary>
        /// 默认构造函数，使用默认参数
        /// </summary>
        public 参数管理器(IMyTerminalBlock block)
        {
            // 初始化参数注册系统
            注册所有参数();

            // 初始化参数管理器（可以从Me.CustomData读取配置）
            string 自定义数据 = block.CustomData;
            if (!string.IsNullOrWhiteSpace(自定义数据))
            {
                解析配置字符串(自定义数据);
                block.CustomData = 生成配置字符串();
            }
            else block.CustomData = 生成配置字符串();
        }

        /// <summary>
        /// 从自定义数据字符串加载参数配置
        /// </summary>
        /// <param name="配置字符串">包含参数配置的字符串</param>
        public 参数管理器(string 配置字符串)
        {
            // 初始化参数注册系统
            注册所有参数();

            解析配置字符串(配置字符串);
        }

        #endregion

        #region 配置解析方法

        /// <summary>
        /// 从配置字符串解析参数
        /// </summary>
        /// <param name="配置字符串">配置字符串</param>
        private void 解析配置字符串(string 配置字符串)
        {
            if (string.IsNullOrWhiteSpace(配置字符串))
                return;

            string[] 行数组 = 配置字符串.Split('\n');
            foreach (string 行 in 行数组)
            {
                string 处理行 = 行.Trim();
                if (string.IsNullOrEmpty(处理行) || 处理行.StartsWith("//") || 处理行.StartsWith("#"))
                    continue;

                string[] 键值对 = 处理行.Split('=');
                if (键值对.Length != 2)
                    continue;

                string 键 = 键值对[0].Trim();
                string 值 = 键值对[1].Trim();

                尝试设置参数(键, 值);
            }
        }

        /// <summary>
        /// 尝试设置指定的参数（基于委托注册系统）
        /// </summary>
        /// <param name="参数名">参数名</param>
        /// <param name="参数值">参数值字符串</param>
        private void 尝试设置参数(string 参数名, string 参数值)
        {
            try
            {
                // 通过参数注册表查找对应的设置委托
                if (参数注册表.ContainsKey(参数名))
                {
                    参数注册表[参数名].设置值(参数值);
                }
                // 未知参数会被自动忽略
            }
            catch (Exception)
            {
                // 参数解析失败时忽略，保持默认值
            }
        }

        #endregion

        #region 配置输出方法

        /// <summary>
        /// 生成当前参数配置的字符串（基于委托注册系统）
        /// </summary>
        /// <returns>参数配置字符串</returns>
        public string 生成配置字符串()
        {
            var 配置 = new StringBuilder();
            配置.AppendLine("// 参数配置文件");
            配置.AppendLine("// 不要修改任何参数，除非你知道以下三件事：");
            配置.AppendLine("// 是什么，如何工作，可能的影响");

            // 遍历参数注册表，生成配置
            foreach (var kvp in 参数注册表)
            {
                string 参数名 = kvp.Key;
                参数描述符 描述符 = kvp.Value;
                string 参数值 = 描述符.获取值();

                // 检查是否应该显示该参数
                bool 应该显示 = !描述符.空值时隐藏 || !string.IsNullOrWhiteSpace(参数值);

                if (应该显示)
                {
                    // 添加参数描述（如果有）
                    if (!string.IsNullOrEmpty(描述符.描述))
                    {
                        配置.AppendLine($"// {描述符.描述}");
                    }

                    // 添加参数配置行
                    配置.AppendLine($"{参数名} = {参数值}");
                    配置.AppendLine();
                }
            }

            return 配置.ToString();
        }

        #endregion
    }    
    /// <summary>
    /// 参数描述符 - 存储参数的访问委托和元数据
    /// </summary>
    public class 参数描述符
    {
        /// <summary>
        /// 获取参数值的委托（转换为字符串）
        /// </summary>
        public Func<string> 获取值 { get; set; }

        /// <summary>
        /// 设置参数值的委托（从字符串解析）
        /// </summary>
        public Action<string> 设置值 { get; set; }

        /// <summary>
        /// 参数描述
        /// </summary>
        public string 描述 { get; set; }

        /// <summary>
        /// 当参数值为null或空时是否隐藏该参数
        /// </summary>
        public bool 空值时隐藏 { get; set; }

        public 参数描述符(Func<string> 获取值, Action<string> 设置值, string 描述 = "", bool 空值时隐藏 = false)
        {
            this.获取值 = 获取值;
            this.设置值 = 设置值;
            this.描述 = 描述;
            this.空值时隐藏 = 空值时隐藏;
        }
    }
}
