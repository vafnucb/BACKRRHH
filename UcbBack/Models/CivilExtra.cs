using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    [CustomSchema("CivilExtra")]
    [Table("CivilExtra")]
    public class CivilExtra
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int CivilId { get; set; }

        [MaxLength(100)]
        public string BankName { get; set; }

        [MaxLength(50)]
        public string BankAccountNumber { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}