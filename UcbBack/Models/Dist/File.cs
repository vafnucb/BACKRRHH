using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using UcbBack.Models.Auth;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models.Dist
{
    [CustomSchema("Dist_File")]
    public class Dist_File
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { set; get; }

        [Required]
        public DateTime UploadedDate { get; set; }

        [MaxLength(150, ErrorMessage = "Cadena de texto muy grande")]
        [Required]
        public string Name { get; set; }

        
        public Dist_FileType DistFileType { get; set; }
        [Required]
        public int DistFileTypeId { get; set; }

        [MaxLength(15, ErrorMessage = "Cadena de texto muy grande")]
        [Required]
        public string State { get; set; }

        [Required]
        public Dist_Process DistProcess { get; set; }
        public long DistProcessId { get; set; }

        
        public CustomUser CustomUser { get; set; }
        [Required]
        public int CustomUserId { get; set; }
        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_Dist_File_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}