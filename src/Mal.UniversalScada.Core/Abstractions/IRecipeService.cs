using Mal.UniversalScada.Core.Models;

namespace Mal.UniversalScada.Core.Abstractions;

/// <summary>
/// 工艺配方单项条目模型
/// </summary>
public record RecipeItem(string TagId, object TargetValue, string Description);

/// <summary>
/// 工艺配方模型（包含产品型号对应的一组参数设定集）
/// </summary>
public class RecipeModel
{
    /// <summary>
    /// 配方唯一 ID
    /// </summary>
    public string RecipeId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 配方名称 (如: "型号A-高速生产参数", "产品B-低温固化参数")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 适用设备 ID
    /// </summary>
    public string TargetDeviceId { get; set; } = string.Empty;

    /// <summary>
    /// 版本号
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// 配方参数条目清单
    /// </summary>
    public List<RecipeItem> Items { get; set; } = new();

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime UpdatedTime { get; set; } = DateTime.Now;
}

/// <summary>
/// 配方下发执行与校验结果模型
/// </summary>
public record RecipeApplyResult(bool IsSuccess, string Message, IReadOnlyDictionary<string, WriteResult> DetailResults);

/// <summary>
/// 工艺配方管理服务接口。
/// 负责产线参数配方的增删改查、导入导出、以及一键同步下发到下位机并执行回读比对校验。
/// </summary>
public interface IRecipeService
{
    /// <summary>
    /// 获取指定设备关联的所有配方清单
    /// </summary>
    /// <param name="deviceId">目标设备 ID</param>
    Task<IReadOnlyList<RecipeModel>> GetRecipesByDeviceAsync(string deviceId);

    /// <summary>
    /// 保存或更新配方
    /// </summary>
    /// <param name="recipe">配方对象</param>
    Task SaveRecipeAsync(RecipeModel recipe);

    /// <summary>
    /// 删除配方
    /// </summary>
    /// <param name="recipeId">配方 ID</param>
    Task DeleteRecipeAsync(string recipeId);

    /// <summary>
    /// 将配方参数一键下发至下位机。
    /// 内部调用优先级调度引擎，批量写入各个寄存器，并执行下发后回读比对校验，确保参数准确入盘生效。
    /// </summary>
    /// <param name="recipeId">要下发的配方 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>下发与回读校验详细结果</returns>
    Task<RecipeApplyResult> ApplyRecipeToDeviceAsync(string recipeId, CancellationToken ct = default);
}
