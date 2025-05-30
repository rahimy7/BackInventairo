using System.ComponentModel.DataAnnotations.Schema;

namespace InventarioAPI.Models
{
    [Table("View_ProductosLI")]
    public class ProductView
    {
        [Column("No_")]
        public string Code { get; set; } = string.Empty;

        [Column("Description")]
        public string Description1 { get; set; } = string.Empty;

        [Column("Description 2")]
        public string Description2 { get; set; } = string.Empty;

        [Column("Division Code")]
        public string DivisionCode { get; set; } = string.Empty;

        [Column("Division")]
        public string Division { get; set; } = string.Empty;

        [Column("Item Category Code")]
        public string CategoryCode { get; set; } = string.Empty;

        [Column("Categoria")]
        public string Category { get; set; } = string.Empty;

        [Column("Product Group Code")]
        public string GroupCode { get; set; } = string.Empty;

        [Column("Grupo")]
        public string Group { get; set; } = string.Empty;

        [Column("Codigo Subgrupo")]
        public string SubGroupCode { get; set; } = string.Empty;

        [Column("SubGrupo")]
        public string SubGroup { get; set; } = string.Empty;

        [Column("MarcaCodigo")]
        public string BrandCode { get; set; } = string.Empty;

        [Column("Marca")]
        public string Brand { get; set; } = string.Empty;

       [Column("Blocked")]
public byte Blocked { get; set; } // o public int si prefieres

    }
}
