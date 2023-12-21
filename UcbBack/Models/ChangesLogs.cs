using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    [CustomSchema("ChangesLogs")]
    public class ChangesLogs
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public string Object { get; set; }
        public string ObjectId { get; set; }
        public string Property { get; set; }
        public string Was { get; set; }
        public string Is { get; set; }



        public void AddChangesLog(dynamic oldRecord, dynamic newRecod, List<string> JustThis = null)
        {
            var oType = oldRecord.GetType();
            var obj = oType.Name;

            foreach (var oProperty in oType.GetProperties())
            {
                var oOldValue = oProperty.GetValue(oldRecord, null);
                var oNewValue = oProperty.GetValue(newRecod, null);
                // this will handle the scenario where either value is null
                if (!object.Equals(oOldValue, oNewValue))
                {
                    if (JustThis != null)
                    {
                        if (JustThis.Contains(oProperty.Name))
                        {
                            // Handle the display values when the underlying value is null
                            var sOldValue = oOldValue == null ? "null" : oOldValue.ToString();
                            var sNewValue = oNewValue == null ? "null" : oNewValue.ToString();

                            var log = new ChangesLogs();
                            log.Is = sNewValue;
                            log.Was = sOldValue;
                            log.Property = oProperty.Name;
                            log.ObjectId = oldRecord.Id.ToString();
                            log.Object = obj;
                            log.addLog();
                        }
                    }
                    else
                    {
                        // Handle the display values when the underlying value is null
                        var sOldValue = oOldValue == null ? "null" : oOldValue.ToString();
                        var sNewValue = oNewValue == null ? "null" : oNewValue.ToString();

                        var log = new ChangesLogs();
                        log.Is = sNewValue;
                        log.Was = sOldValue;
                        log.Property = oProperty.Name;
                        log.ObjectId = oldRecord.Id.ToString();
                        log.Object = obj;
                        log.addLog();
                    }
                }
            }
        }

        public void addLog()
        {
            var _context = new ApplicationDbContext();
            this.Id = GetNextId(_context);
            _context.ChangesLogses.Add(this);
            _context.SaveChanges();
        }

        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_ChangesLogs_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}