using System.Security.Cryptography;
using ClassFabric.RemoteBroadcast.Models;
using Microsoft.Extensions.Logging;

namespace ClassFabric.RemoteBroadcast.Services;

/// <summary>
/// R2：接入码管理——多码并存、创建（必填备注）、撤销即时失效、登记绑定教师（覆盖式）。
/// </summary>
public class AccessCodeManager(ConfigStore store, ILogger<AccessCodeManager> logger)
{
    public AccessCodeEntry Create(string note)
    {
        if (string.IsNullOrWhiteSpace(note) || note.Trim().Length > 30)
        {
            throw new ArgumentException("接入码备注不能为空且不超过 30 字符。");
        }

        var entry = new AccessCodeEntry
        {
            // 16 字节随机 → 32 位 hex：够防猜，二维码塞得下，URL 也好看。
            Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
            Note = note.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
        };
        store.Update(c => c.AccessCodes.Add(entry));
        logger.LogInformation("[RemoteBroadcast] 创建接入码（备注：{Note}）", entry.Note);
        return entry;
    }

    /// <summary>撤销：从列表移除，即刻失效，不影响其他接入码。</summary>
    public bool Revoke(string token)
    {
        // 撤销就是物理删除，不留「已撤销」的僵尸码——这样 TryGet 的判断逻辑可以一直保持傻白甜。
        var removed = false;
        store.Update(c => removed = c.AccessCodes.RemoveAll(x => x.Token == token) > 0);
        if (removed)
        {
            logger.LogInformation("[RemoteBroadcast] 撤销接入码（{Token}…）", token[..8]);
        }

        return removed;
    }

    /// <summary>登记回传绑定：同一码被重新登记时覆盖（以最后登记者为准）。</summary>
    public bool Bind(string token, string teacherName)
    {
        // 覆盖式绑定是故意的：老师换人重新扫码登记，绑定的就该是新老师（任务书 v2.2 定稿）。
        var found = false;
        store.Update(c =>
        {
            var entry = c.AccessCodes.FirstOrDefault(x => x.Token == token);
            if (entry == null)
            {
                return;
            }

            entry.BoundTeacher = teacherName;
            entry.BoundAtUtc = DateTime.UtcNow;
            found = true;
        });
        return found;
    }

    public AccessCodeEntry? TryGet(string token)
    {
        return store.Snapshot().AccessCodes.FirstOrDefault(x => x.Token == token);
    }

    public List<AccessCodeEntry> GetAll()
    {
        return store.Snapshot().AccessCodes;
    }
}
