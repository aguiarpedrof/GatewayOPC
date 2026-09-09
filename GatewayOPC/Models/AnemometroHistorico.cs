using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GatewayOPC.Models
{
    [Table("anemometro_historico", Schema = "tracker")]
    public class AnemometroHistorico
    {
        [Key]
        [Required]
        public int id { get; set; }

        public Anemometro? anemometro { get; set; }

        [Key]
        [Required]
        public DateTime leitura { get; set; }

        [Required]
        public short status { get; set; }

        [Required]
        public float velocidade_vento { get; set; }

        [Required]
        public float direcao_vento { get; set; }

        [Required]
        public float temperatura { get; set; }

        [Required]
        public float pressao { get; set; }

        [Required]
        public short umidade { get; set; }
    }
}
