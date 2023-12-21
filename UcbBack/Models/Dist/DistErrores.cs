using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models.Dist
{
    [CustomSchema("Error")]
    public class Error
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { set; get; }

        [MaxLength(100, ErrorMessage = "Cadena de texto muy grande")]
        [Required]
        public string Name { get; set; }

        [MaxLength(250, ErrorMessage = "Cadena de texto muy grande")]
        [Required]
        public string Description { get; set; }

        [MaxLength(1, ErrorMessage = "Cadena de texto muy grande")]
        [Required]
        public string Type { get; set; }

    }
}