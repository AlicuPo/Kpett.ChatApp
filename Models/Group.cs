using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kpett.ChatApp.Models;

public partial class Group
{
    public string Id { get; set; } = null!;

    public string? Name { get; set; }

    public string? AvatarUrl { get; set; }

    [Column("CoverUrl")]
    public string? CoverImageUrl { get; set; }

    public string? Description { get; set; }

    public string? Type { get; set; }  // hidden / private / public 

    public DateTime CreatedAt { get; set; }

    public string? CreatedByUserId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? Status { get; set; }

    public string? UpdatedByUserId { get; set; }

    public string? OwnerUserId { get; set; } 
    public bool PostApproval { get; set; } = false;

    public bool MemberApproval { get; set; } = false;

    public string? WhoCanPost { get; set; }  //  "anyone" / "admin_mod" / "admin_only"
    public string? WhoCanInvite { get; set; }  //  "anyone" / "admin_mod" / "admin_only"

    public string? Language { get; set; }
}
