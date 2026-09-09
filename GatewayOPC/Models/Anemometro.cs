using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GatewayOPC.Models
{
    [Table("anemometro", Schema = "tracker")]
    public class Anemometro
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id { get; set; }

        [Required]
        public string eui { get; set; } = string.Empty;

        [Required]
        public string id_usuario { get; set; } = string.Empty;

        public DateTime? leitura { get; set; }

        public float? velocidade_vento { get; set; }

        public float? direcao_vento { get; set; }

        public float? temperatura { get; set; }

        public float? pressao { get; set; }

        public short? umidade { get; set; }

        public short? status { get; set; }

        [Required]
        public decimal latitude { get; set; }

        [Required]
        public decimal longitude { get; set; }

        [Required]
        public string ip { get; set; } = string.Empty;

        [Required]
        public short porta { get; set; }

        [Required]
        public short id_subcampo { get; set; } = 1;

        public bool ativo { get; set; } = true;

        [Required]
        public int periodo_coleta { get; set; }

        [Required]
        public bool holding { get; set; }

        [Required]
        public short registrador_velocidade { get; set; } = 0;

        public short? registrador_direcao { get; set; }

        public short? registrador_temperatura { get; set; }

        public short? registrador_pressao { get; set; }

        public short? registrador_umidade { get; set; }

        public Subcampo? Subcampo { get; set; }

        public ICollection<AnemometroHistorico> Anemometro_historicos { get; set; } = new List<AnemometroHistorico>();
    }
}
