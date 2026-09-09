using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GatewayOPC.Models
{
    [Table("tracker", Schema = "tracker")]
    public class Tracker
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id { get; set; }

        [Required]
        public string eui { get; set; } = string.Empty;

        [Required]
        public string id_usuario { get; set; } = string.Empty;

        [Required]
        public int gateway_id { get; set; }

        public Gateway? gateway { get; set; }

        public DateTime? leitura { get; set; }

        public short? modo { get; set; }

        public float? tensao_bateria { get; set; }

        public float? tensao_painel { get; set; }

        public float? corrente_painel { get; set; }

        public float? corrente_motor { get; set; }

        public float? corrente_bateria { get; set; }

        public float? inclinacao_atual { get; set; }

        public float? inclinacao_alvo { get; set; }

        public short? tipo_movimento { get; set; }

        public short? soc { get; set; }

        public short? status { get; set; }

        public short? erro { get; set; }

        public short? rssi { get; set; }

        public short? snr { get; set; }

        public float? pitch { get; set; }

        [Required]
        public decimal latitude { get; set; }

        [Required]
        public decimal longitude { get; set; }

        [Required]
        public float installation_azimuth { get; set; }

        [Required]
        public float installation_tilt { get; set; }

        [Required]
        public string inverter { get; set; } = string.Empty;

        [Required]
        public float backtracking_distance { get; set; }

        [Required]
        public float slope_east { get; set; }

        [Required]
        public float slope_west { get; set; }

        [Required]
        public float module_width { get; set; }

        public float? temperatura_bateria { get; set; }

        public float? umidade_bateria { get; set; }

        public float? temperatura_painel { get; set; }

        public bool automatico { get; set; }

        public short id_subcampo { get; set; }

        public bool ativo { get; set; } = true;

        [NotMapped]
        public string descricaoMovimento =>
            tipo_movimento == 0 ? "Parado" :
            tipo_movimento == 1 ? "Leste" :
            tipo_movimento == 2 ? "Oeste" : "Desconhecido";

        [NotMapped]
        public bool conectado => ativo && leitura.HasValue && ((TimeSpan)(DateTime.Now - leitura.Value)).TotalMinutes < 10;

        [NotMapped]
        public BitArray Erros => new BitArray(BitConverter.GetBytes(erro.HasValue ? (int)erro.Value : 0));

        [NotMapped]
        public bool ErroTensaoMaximaBateria => erro.HasValue && erro != 0 && Erros.Get(0);

        [NotMapped]
        public bool ErroTensaoMinimaBateria => erro.HasValue && erro != 0 && Erros.Get(1);

        [NotMapped]
        public bool ErroSOCMinimo => erro.HasValue && erro != 0 && Erros.Get(2);

        [NotMapped]
        public bool ErroCorrenteMaximaDescarga => erro.HasValue && erro != 0 && Erros.Get(3);

        [NotMapped]
        public bool ErroCorrenteMaximaCarga => erro.HasValue && erro != 0 && Erros.Get(4);

        [NotMapped]
        public bool ErroCorrenteMaximaDriverMotor => erro.HasValue && erro != 0 && Erros.Get(5);

        [NotMapped]
        public bool ErroSobrecorrenteMotor => erro.HasValue && erro != 0 && Erros.Get(6);

        [NotMapped]
        public bool ErroDriverMotor => erro.HasValue && erro != 0 && Erros.Get(7);

        [NotMapped]
        public bool ErroDriverBateria => erro.HasValue && erro != 0 && Erros.Get(8);

        [NotMapped]
        public bool ErroTensaoMinimaPainel => erro.HasValue && erro != 0 && Erros.Get(9);

        [NotMapped]
        public bool ErroTensaoMaximaPainel => erro.HasValue && erro != 0 && Erros.Get(10);

        [NotMapped]
        public bool ErroCorrenteMaximaPainel => erro.HasValue && erro != 0 && Erros.Get(11);

        [NotMapped]
        public bool ErroInclinacaoMinima => erro.HasValue && erro != 0 && Erros.Get(12) && !Erros.Get(13);

        [NotMapped]
        public bool ErroInclinacaoMaxima => erro.HasValue && erro != 0 && !Erros.Get(12) && Erros.Get(13);

        [NotMapped]
        public bool ErroReferencia => erro.HasValue && erro != 0 && Erros.Get(12) && Erros.Get(13);

        [NotMapped]
        public bool ErroSemCorrenteMotor => erro.HasValue && erro != 0 && Erros.Get(14) && !Erros.Get(15);

        [NotMapped]
        public bool ErroReferenciaNaoAlcancada => erro.HasValue && erro != 0 && !Erros.Get(14) && Erros.Get(15);

        [NotMapped]
        public bool ErroAcelerometro => erro.HasValue && erro != 0 && Erros.Get(14) && Erros.Get(15);

        [NotMapped]
        public bool comErro => conectado && erro.HasValue && erro.Value != 0 &&
            (ErroCorrenteMaximaCarga || ErroCorrenteMaximaDescarga || ErroAcelerometro || ErroCorrenteMaximaDriverMotor ||
            ErroCorrenteMaximaPainel || ErroDriverBateria || ErroDriverMotor || ErroInclinacaoMaxima || ErroInclinacaoMinima ||
            ErroReferencia || ErroReferenciaNaoAlcancada || ErroSemCorrenteMotor || ErroSobrecorrenteMotor || ErroSOCMinimo ||
            ErroTensaoMaximaBateria || ErroTensaoMaximaPainel || ErroTensaoMinimaBateria);

        [NotMapped]
        public bool semGeracao => conectado && erro.HasValue && erro.Value != 0 && !comErro;

        [NotMapped]
        public string descricaoStatus => !ativo ? "Desativado" : (!conectado ? "Desconectado" : (comErro ? "ERRO" : (modo >= 0 && modo <= 4 ? "Tracking" : "Manual")));

        [NotMapped]
        public string descricaoModo =>
            !ativo ? "Desativado" :
            !conectado ? "Desconectado" :
            modo switch
            {
                0 => "Tracking",
                1 => "DAL",
                2 => "STOW",
                3 => "Night",
                4 => "STOP",
                5 => "STOW",
                6 => "Move East",
                7 => "Move West",
                8 => "Target Slope",
                9 => "Cleaning",
                10 => "Emergency",
                _ => ""
            };

        public Subcampo? Subcampo { get; set; }

        public ICollection<TrackerHistorico> Tracker_historicos { get; set; } = new List<TrackerHistorico>();
    }
}
