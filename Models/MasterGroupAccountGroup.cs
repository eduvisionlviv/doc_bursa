namespace doc_bursa.Models
{
    /// <summary>
    /// Проміжна сутність для зв'язку MasterGroup ↔ AccountGroup з композитним ключем.
    /// </summary>
    public class MasterGroupAccountGroup
    {
        public int Id { get; set; }

        public int MasterGroupId { get; set; }
        public MasterGroup MasterGroup { get; set; } = null!;

        public int AccountGroupId { get; set; }
        public AccountGroup AccountGroup { get; set; } = null!;
    }
}
