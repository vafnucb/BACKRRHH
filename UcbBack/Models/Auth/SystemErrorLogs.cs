using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models.Auth
{
    [CustomSchema("SystemErrorLogs")]
    public class SystemErrorLogs
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public int UserId { get; set; }
        public string ObjectType { get; set; }
        public int ObjectId { get; set; }
        public int ErrorId { get; set; }
        public string ExceptionMessage { get; set; }
        public bool Inspected { get; set; }
        public DateTime Created { get; set; }

        public SystemErrorLogs(CustomUser user,dynamic record, string exception)
        {
            var _context = new ApplicationDbContext();
            var oType = record.GetType();
            var obj = oType.Name;
            this.Id = GetNextId(_context);
            this.UserId = user.Id;
            this.ObjectId = record.Id;
            this.ObjectType = obj;
            this.ExceptionMessage = exception;
            this.Inspected = false;
            this.Created = DateTime.Now;
            _context.SystemErrorLogses.Add(this);
            _context.SaveChanges();
        }
        public SystemErrorLogs(CustomUser user,dynamic record, int ErrorId)
        {
            var _context = new ApplicationDbContext();
            var oType = record.GetType();
            var obj = oType.Name;
            this.Id = GetNextId(_context);
            this.UserId = user.Id;
            this.ObjectId = record.Id;
            this.ObjectType = obj;
            this.ErrorId = ErrorId;
            this.Inspected = false;
            this.Created = DateTime.Now;
            _context.SystemErrorLogses.Add(this);
            _context.SaveChanges();
        }


        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_SystemErrorLogs_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}