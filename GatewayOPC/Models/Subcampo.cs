using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GatewayOPC.Models
{
    [Table("subcampo", Schema = "tracker")]
    public class Subcampo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public short id { get; set; }

        [Required]
        public string nome { get; set; } = string.Empty;

        public ICollection<SubcampoPontos> Subcampo_pontos { get; set; } = new List<SubcampoPontos>();
        public ICollection<Anemometro> Anemometros { get; set; } = new List<Anemometro>();
        public ICollection<Gateway> Gateways { get; set; } = new List<Gateway>();
        public ICollection<Tracker> Trackers { get; set; } = new List<Tracker>();
    }
}
