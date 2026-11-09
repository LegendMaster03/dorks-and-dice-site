namespace dorks_and_dice_site.Models
{
    /// <summary>
    /// Request diagnostics for the shared framework error view; no mode-owned
    /// content or account details are included in this response model.
    /// </summary>

    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
