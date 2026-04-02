using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Models;

public partial class Follow
{
    public string Id { get; set; } = null!;

    [MaxLength(450)]
    public string FollowerId { get; set; } = null!;

    [MaxLength(450)]
    public string FolloweeId { get; set; } = null!;

    public bool? IsMuted { get; set; }

    public DateTime CreatedAt { get; set; }

}
