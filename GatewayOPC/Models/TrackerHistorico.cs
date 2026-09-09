using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GatewayOPC.Models
{
    [Table("tracker_historico", Schema = "tracker")]
    public class TrackerHistorico
    {
        [Key]
        [Required]
        public int id { get; set; }

        public Tracker? tracker { get; set; }

        [Key]
        [Required]
        public DateTime leitura { get; set; }

        [Required]
        public short modo { get; set; }

        [Required]
        public float tensao_bateria { get; set; }

        [Required]
        public float tensao_painel { get; set; }

        [Required]
        public float corrente_painel { get; set; }

        [Required]
        public float corrente_motor { get; set; }

        [Required]
        public float corrente_bateria { get; set; }

        [Required]
        public float inclinacao_atual { get; set; }

        [Required]
        public float inclinacao_alvo { get; set; }

        [Required]
        public short tipo_movimento { get; set; }

        [Required]
        public short soc { get; set; }

        [Required]
        public short status { get; set; }

        [Required]
        public short erro { get; set; }

        [Required]
        public short rssi { get; set; }

        [Required]
        public short snr { get; set; }

        public float? pitch { get; set; }

        public float? temperatura_bateria { get; set; }

        public float? umidade_bateria { get; set; }

        public float? temperatura_painel { get; set; }
    }
}
