using System;
using System.Collections.Generic;

namespace Kpett.ChatApp.Models;

public partial class MessageDetail
{
    public long MessageId { get; set; }

    public string? Content { get; set; }

    public string? Color { get; set; }
}
