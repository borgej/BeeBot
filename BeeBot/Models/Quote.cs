using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace YTBot.Models
{
    public class Quote
    {
        public int Id { get; set; }
        [Column(TypeName = "VARCHAR")]
        [StringLength(128)]
        public string QuoteMsg { get; set; }
        [Column(TypeName = "VARCHAR")]
        [StringLength(128)]
        public string QuoteBy { get; set; }
        public DateTime? QuoteAdded { get; set; }
        public int Nr { get; set; }
    }
}