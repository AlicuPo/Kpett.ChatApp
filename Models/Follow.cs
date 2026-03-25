using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class Follow
{
    public string Id { get; set; } = null!;

    public string FollowerId { get; set; } = null!;

    public string FolloweeId { get; set; } = null!;

    public bool? IsMuted { get; set; }

    public DateTime? CreatedAt { get; set; }

}
