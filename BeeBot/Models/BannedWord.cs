using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace YTBot.Models
{
    public class BannedWord
    {
        public int Id { get; set; }
        [Column(TypeName = "VARCHAR")]
        [StringLength(128)]
        public string Word { get; set; }
    }
}