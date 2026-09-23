using System.Text.Json;
using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Core.Models;
using Microsoft.Data.Sqlite;

namespace LeslieCleaner.Data;

/// <summary>
/// 残留规则种子导入器。从嵌入资源 residual-rules.seed.json 读取规则并
/// upsert 进 residual_rules 表；幂等，可重复执行。规则内容在阶段 16 扩充至 1000+。
/// </summary>
public sealed class ResidualRuleImporter
{
    private const string SeedResourceName = "LeslieCleaner.Data.Seeds.residual-rules.seed.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// 将嵌入种子规则导入 residual_rules 表。
    /// </summary>
    /// <param name="database">已初始化的数据库。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>导入的规则条数。</returns>
    /// <exception cref="InvalidOperationException">种子资源缺失或损坏时抛出。</exception>
    public async Task<int> ImportAsync(IDatabase database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);

        IReadOnlyList<ResidualRule> rules = await LoadSeedRulesAsync(cancellationToken).ConfigureAwait(false);

        const string upsertSql = """
            INSERT OR REPLACE INTO residual_rules (publisher, product, paths, registry)
            VALUES ($publisher, $product, $paths, $registry);
            """;

        IEnumerable<IReadOnlyList<SqliteParameter>> batch = rules.Select(static rule => (IReadOnlyList<SqliteParameter>)
        [
            new SqliteParameter("$publisher", rule.Publisher),
            new SqliteParameter("$product", rule.Product ?? string.Empty),
            new SqliteParameter("$paths", JsonSerializer.Serialize(rule.Paths, JsonOptions)),
            new SqliteParameter("$registry", JsonSerializer.Serialize(rule.RegistryKeys, JsonOptions)),
        ]);

        await database.ExecuteBatchAsync(upsertSql, batch, cancellationToken).ConfigureAwait(false);
        return rules.Count;
    }

    /// <summary>
    /// 从 residual_rules 表读取全部规则。
    /// </summary>
    /// <param name="database">已初始化的数据库。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<IReadOnlyList<ResidualRule>> LoadAllAsync(IDatabase database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        return database.QueryAsync(
            "SELECT publisher, product, paths, registry FROM residual_rules;",
            static reader => new ResidualRule(
                reader.GetString(0),
                reader.GetString(1) is { Length: > 0 } product ? product : null,
                JsonSerializer.Deserialize<IReadOnlyList<string>>(reader.GetString(2), JsonOptions) ?? [],
                JsonSerializer.Deserialize<IReadOnlyList<string>>(reader.GetString(3), JsonOptions) ?? []),
            null,
            cancellationToken);
    }

    /// <summary>
    /// 按发布者/产品查询规则（先精确匹配 publisher+product，再回退发布者级规则）。
    /// </summary>
    /// <param name="database">已初始化的数据库。</param>
    /// <param name="publisher">发布者。</param>
    /// <param name="product">产品名，可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<IReadOnlyList<ResidualRule>> QueryAsync(
        IDatabase database, string? publisher, string? product, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        publisher = string.IsNullOrWhiteSpace(publisher) ? null : publisher.Trim();
        product = string.IsNullOrWhiteSpace(product) ? null : product.Trim();
        if (publisher is null && product is null)
        {
            return Task.FromResult<IReadOnlyList<ResidualRule>>([]);
        }

        return database.QueryAsync(
            """
            SELECT publisher, product, paths, registry FROM residual_rules
            WHERE (
                $publisher IS NOT NULL
                AND (publisher = $publisher COLLATE NOCASE
                     OR publisher LIKE $publisherLike COLLATE NOCASE)
                AND ($product IS NULL OR product = '' OR product = $product COLLATE NOCASE)
            )
            OR (
                $publisher IS NULL
                AND $product IS NOT NULL
                AND product = $product COLLATE NOCASE
            );
            """,
            static reader => new ResidualRule(
                reader.GetString(0),
                reader.GetString(1) is { Length: > 0 } productName ? productName : null,
                JsonSerializer.Deserialize<IReadOnlyList<string>>(reader.GetString(2), JsonOptions) ?? [],
                JsonSerializer.Deserialize<IReadOnlyList<string>>(reader.GetString(3), JsonOptions) ?? []),
            [
                new SqliteParameter("$publisher", (object?)publisher ?? DBNull.Value),
                new SqliteParameter("$publisherLike", publisher is null ? DBNull.Value : $"%{publisher}%"),
                new SqliteParameter("$product", (object?)product ?? DBNull.Value),
            ],
            cancellationToken);
    }

    private static async Task<IReadOnlyList<ResidualRule>> LoadSeedRulesAsync(CancellationToken cancellationToken)
    {
        System.Reflection.Assembly assembly = typeof(ResidualRuleImporter).Assembly;
        await using Stream stream = assembly.GetManifestResourceStream(SeedResourceName)
            ?? throw new InvalidOperationException($"残留规则种子资源缺失: {SeedResourceName}");

        List<ResidualRuleSeed>? seeds = await JsonSerializer
            .DeserializeAsync<List<ResidualRuleSeed>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        if (seeds is null || seeds.Count == 0)
        {
            throw new InvalidOperationException($"残留规则种子资源为空或损坏: {SeedResourceName}");
        }

        return seeds.ConvertAll(static seed => new ResidualRule(seed.Publisher, seed.Product, seed.Paths, seed.Registry));
    }

    private sealed record ResidualRuleSeed(
        string Publisher,
        string? Product,
        IReadOnlyList<string> Paths,
        IReadOnlyList<string> Registry);
}
