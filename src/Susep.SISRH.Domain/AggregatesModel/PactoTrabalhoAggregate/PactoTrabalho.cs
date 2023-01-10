﻿using Newtonsoft.Json;
using Susep.SISRH.Domain.AggregatesModel.CatalogoAggregate;
using Susep.SISRH.Domain.AggregatesModel.PessoaAggregate;
using Susep.SISRH.Domain.AggregatesModel.PlanoTrabalhoAggregate;
using Susep.SISRH.Domain.AggregatesModel.UnidadeAggregate;
using Susep.SISRH.Domain.Enums;
using Susep.SISRH.Domain.Exceptions;
using Susep.SISRH.Domain.Helpers;
using SUSEP.Framework.SeedWorks.Domains;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;

namespace Susep.SISRH.Domain.AggregatesModel.PactoTrabalhoAggregate
{
    /// <summary>
    /// Representa um plano de trabalho pactuado entre um servidor e o seu chefe
    /// </summary>
    public class PactoTrabalho : Entity
    {

        public Guid PactoTrabalhoId { get; private set; }
        public Guid PlanoTrabalhoId { get; private set; }
        public Int64 PessoaId { get; private set; }
        public Int64 UnidadeId { get; private set; }
        public DateTime DataInicio { get; private set; }
        public DateTime DataFim { get; private set; }
        public Int32 ModalidadeExecucaoId { get; private set; }
        public Int32 SituacaoId { get; private set; }
        public string TermoAceite { get; private set; }
        public Int32 CargaHorariaDiaria { get; private set; }
        public Decimal? PercentualExecucao { get; private set; }
        public Decimal? RelacaoPrevistoRealizado { get; private set; }
        public Int32 TempoTotalDisponivel { get; private set; }

        public Int32? TipoFrequenciaTeletrabalhoParcialId { get; private set; }
        public Int32? QuantidadeDiasFrequenciaTeletrabalhoParcial { get; private set; }


        public PlanoTrabalho PlanoTrabalho { get; private set; }
        public Pessoa Pessoa { get; private set; }
        public Unidade Unidade { get; private set; }
        public CatalogoDominio Situacao { get; private set; }

        public List<PactoTrabalhoAtividade> Atividades { get; private set; }
        public List<PactoTrabalhoSolicitacao> Solicitacoes { get; private set; }
        public List<PactoTrabalhoHistorico> Historico { get; private set; }
        public List<PactoTrabalhoDeclaracao> Declaracoes { get; private set; }
        public List<PactoTrabalhoInformacao> Informacoes { get; private set; }

        [NotMapped]
        public List<DateTime> DiasNaoUteis { get; set; }

        public PactoTrabalho() { }

        public static PactoTrabalho Criar(Guid planoTrabalhoId, Int64 unidadeId, Int64 pessoaId, Int32 cargaHorariaDiaria, int modalidadeExecucaoId, DateTime dataInicio, DateTime dataFim, String usuarioLogado, List<DateTime> diasNaoUteis, string termoAceite)
        {
            //Cria o pacto de trabalho para ser ajustado pelo chefe da unidade
            var model = new PactoTrabalho()
            {
                PactoTrabalhoId = Guid.NewGuid(),
                PlanoTrabalhoId = planoTrabalhoId,
                UnidadeId = unidadeId,
                PessoaId = pessoaId,
                CargaHorariaDiaria = cargaHorariaDiaria,
                ModalidadeExecucaoId = modalidadeExecucaoId,
                DataInicio = dataInicio,
                DataFim = dataFim,
                Historico = new List<PactoTrabalhoHistorico>(),
                Atividades = new List<PactoTrabalhoAtividade>(),
                TermoAceite = termoAceite
            };

            model.ValidarDatas(dataInicio, dataFim);

            model.DiasNaoUteis = diasNaoUteis;

            model.TempoTotalDisponivel = cargaHorariaDiaria * WorkingDays.DiffDays(dataInicio, dataFim, diasNaoUteis, false);

            //OK
            model.AlterarSituacao((int)SituacaoPactoTrabalhoEnum.Rascunho, usuarioLogado, null);

            return model;
        }

        public void AlterarPeriodo(DateTime dataFim)
        {
            AlterarPeriodo(this.DataInicio, dataFim);
        }

        public void AlterarPeriodo(DateTime dataInicio, DateTime dataFim)
        {
            ValidarDatas(dataInicio, dataFim);

            this.DataInicio = dataInicio;
            this.DataFim = dataFim;
            this.TempoTotalDisponivel = this.CargaHorariaDiaria * WorkingDays.DiffDays(dataInicio, dataFim, DiasNaoUteis, false);

            var atividadesDiarias = this.Atividades.Where(it => it.ItemCatalogo.FormaCalculoTempoItemCatalogoId == (int)FormaCalculoTempoItemCatalogoEnum.PredefinidoPorDia).ToList();
            var quantidadeDias = WorkingDays.DiffDays(this.DataInicio, this.DataFim, this.DiasNaoUteis, false);
            atividadesDiarias.ForEach(it => it.AtualizarTempoPrevistoTotal(quantidadeDias, it.TempoPrevistoPorItem));
            
            this.AtualizarPercentualExecucao();
        }

        public void AlterarFrequenciaTeletrabalhoParcial(Int32 tipoFrequenciaTeletrabalhoParcialId, Int32 quantidadeDiasFrequenciaTeletrabalhoParcial)
        {
            TipoFrequenciaTeletrabalhoParcialId = tipoFrequenciaTeletrabalhoParcialId;
            QuantidadeDiasFrequenciaTeletrabalhoParcial = quantidadeDiasFrequenciaTeletrabalhoParcial;
        }

        #region Fluxo de aprovação do pacto

        public void AlterarSituacao(Int32 situacaoId, String responsavelOperacao, String observacoes, Boolean frequenciaPresencialObrigatoria = false)
        {
            if (!PodeAlteracaoSituacao(situacaoId))
                throw new SISRHDomainException("A situação atual do plano não permite mudar para o estado solicitado");

            if (frequenciaPresencialObrigatoria &&
                situacaoId == (int)SituacaoPactoTrabalhoEnum.EnviadoAceite && 
                this.ModalidadeExecucaoId == (int)ModalidadeExecucaoEnum.Semipresencial &&
                (!this.QuantidadeDiasFrequenciaTeletrabalhoParcial.HasValue || !this.TipoFrequenciaTeletrabalhoParcialId.HasValue))
            {
                throw new SISRHDomainException("Na modalidade de teletrabalho parcial, é obrigatório informar o tipo de frequência e a quantidade de dias.");
            }

            //Não deve permitir que a própria pessoa encerre um plano seu sem ter concluído todas as atividades    
            int responsavelOperacaoId = -1;
            Int32.TryParse(responsavelOperacao, out responsavelOperacaoId);
            if (situacaoId == (int)SituacaoPactoTrabalhoEnum.Executado && 
                this.PessoaId == responsavelOperacaoId &&
                this.Atividades.Any(a => a.ItemCatalogo.FormaCalculoTempoItemCatalogoId == (int)FormaCalculoTempoItemCatalogoEnum.PredefinidoPorTarefa &&
                                         a.SituacaoId != (int)SituacaoAtividadePactoTrabalhoEnum.Done))
            {
                throw new SISRHDomainException("O próprio servidor não pode encerrar um plano de trabalho que ainda tem atividades a serem executadas. Favor solicitar que o chefe faça o encerramento.");
            }

            if (this.SituacaoId == (int)Domain.Enums.SituacaoPactoTrabalhoEnum.Executado &&
                situacaoId == (int)Domain.Enums.SituacaoPactoTrabalhoEnum.EmExecucao &&
                this.PessoaId == responsavelOperacaoId)
            {
                throw new SISRHDomainException("O próprio servidor não pode reabrir seu próprio plano de trabalho. Favor solicitar que o chefe faça a reabertura.");
            }

            this.SituacaoId = situacaoId;

            //Quando iniciar ou encerrar a execução
            //  Deve executar alguns procedimentos para permitir que os servidores possam acomparnhar as atividades
            if (SituacaoId == (int)SituacaoPactoTrabalhoEnum.EmExecucao)
            {
                if (DateTime.Now.Date < this.DataInicio)
                    throw new SISRHDomainException("Não é possível iniciar a execução do plano de trabalho antes da data de início");

                this.IniciarExecucao();
            }
            else if (SituacaoId == (int)SituacaoPactoTrabalhoEnum.Aceito)
            {
                this.TermoAceite = this.PlanoTrabalho.TermoAceite;
            }
            else if (SituacaoId == (int)SituacaoPactoTrabalhoEnum.Executado)
            {
                this.EncerrarExecucao();
            }

            this.Historico.Add(PactoTrabalhoHistorico.Criar(this.PlanoTrabalhoId, this.SituacaoId, responsavelOperacao, observacoes));
        }

        private Boolean PodeAlteracaoSituacao(Int32 situacaoId)
        {
            switch (this.SituacaoId)
            {
                case (int)SituacaoPactoTrabalhoEnum.Rascunho:
                    return situacaoId == (int)SituacaoPactoTrabalhoEnum.EnviadoAceite;
                case (int)SituacaoPactoTrabalhoEnum.EnviadoAceite:
                    return situacaoId == (int)SituacaoPactoTrabalhoEnum.Aceito || situacaoId == (int)SituacaoPactoTrabalhoEnum.Rejeitado;
                case (int)SituacaoPactoTrabalhoEnum.Aceito:
                    return situacaoId == (int)SituacaoPactoTrabalhoEnum.EmExecucao;
                case (int)SituacaoPactoTrabalhoEnum.Rejeitado:
                    return situacaoId == (int)SituacaoPactoTrabalhoEnum.Rascunho;
                case (int)SituacaoPactoTrabalhoEnum.EmExecucao:
                    return situacaoId == (int)SituacaoPactoTrabalhoEnum.Executado;
                case (int)SituacaoPactoTrabalhoEnum.Executado:
                    return situacaoId == (int)SituacaoPactoTrabalhoEnum.Concluido ||
                           situacaoId == (int)SituacaoPactoTrabalhoEnum.EmExecucao;
            }

            return true;
        }

        private void IniciarExecucao()
        {
            //ok
            this.AtualizarAtividadesPorDia(SituacaoAtividadePactoTrabalhoEnum.Doing);
            this.ConverterAtividadesAgrupadasParaUnicas();
        }

        private void EncerrarExecucao()
        {
            if (this.DataFim > DateTime.Now.Date)
                this.DataFim = DateTime.Now.Date;
            this.AtualizarAtividadesPorDia(SituacaoAtividadePactoTrabalhoEnum.Done);
        }

        /// <summary>
        /// Muda as atividades que tem mais de um item para que fiquem com somente um para que o analista possa acompanhar cada uma delas
        /// </summary>
        private void ConverterAtividadesAgrupadasParaUnicas()
        {
            var atividadesAgrupadas = this.Atividades.Where(a => a.Quantidade > 1).ToList();

            foreach (var atividade in atividadesAgrupadas)
            {
                var modalidaExecucaoId = atividade.ModalidadeExecucaoId.HasValue ? atividade.ModalidadeExecucaoId.Value : this.ModalidadeExecucaoId;
                for (var index = 1; index < atividade.Quantidade; index++)
                    this.Atividades.Add(PactoTrabalhoAtividade.Criar(atividade.PactoTrabalhoId, atividade.ItemCatalogoId, 1, modalidaExecucaoId, atividade.TempoPrevistoPorItem, atividade.Descricao));
                atividade.Alterar(atividade.ItemCatalogoId, 1, modalidaExecucaoId, atividade.TempoPrevistoPorItem, atividade.Descricao);
            }
        }

        private void AtualizarAtividadesPorDia(SituacaoAtividadePactoTrabalhoEnum situacao)
        {
            var atividadesPorDia = this.Atividades.Where(a => a.ItemCatalogo.FormaCalculoTempoItemCatalogoId == (int)FormaCalculoTempoItemCatalogoEnum.PredefinidoPorDia).ToList();
            atividadesPorDia.ForEach(a =>
            {
                if (situacao == SituacaoAtividadePactoTrabalhoEnum.Done)
                {
                    a.DefinirComoConcluida(this.DataFim, this.DiasNaoUteis, this.CargaHorariaDiaria);
                }
                else
                    a.DefinirComoEmExecucao();
            });
        }

        #endregion

        #region Atividades

        #region Criação, edição e exclusão de atividades

        public void AdicionarAtividade(ItemCatalogo itemCatalogo, int quantidade, int modalidaExecucaoId, decimal tempoPrevistoPorItem, string descricao, IEnumerable<Guid> idsAssuntos, IEnumerable<Guid> idsObjetos)
        {
            VerificarPossibilidadeAlteracao();
            var atividade = PactoTrabalhoAtividade.Criar(this.PactoTrabalhoId, itemCatalogo.ItemCatalogoId, quantidade, modalidaExecucaoId, tempoPrevistoPorItem, descricao);
            if (idsAssuntos != null)
            {
                foreach (var idAssunto in idsAssuntos)
                {
                    atividade.AdicionarAssunto(idAssunto);
                }
            }
            if (idsObjetos != null)
            {
                foreach (var idObjeto in idsObjetos)
                {
                    atividade.AdicionarObjeto(idObjeto);
                }
            }

            this.Atividades.Add(atividade);
            if (itemCatalogo.FormaCalculoTempoItemCatalogoId == (int)FormaCalculoTempoItemCatalogoEnum.PredefinidoPorDia)
                atividade.AtualizarTempoPrevistoTotal(WorkingDays.DiffDays(this.DataInicio, this.DataFim, this.DiasNaoUteis, false));
        }

        public void AlterarAtividade(Guid pactoTrabalhoAtividadeId, ItemCatalogo itemCatalogo, int quantidade, int modalidaExecucaoId, decimal tempoPrevistoPorItem, string descricao, IEnumerable<Guid> idsAssuntos, IEnumerable<Guid> idsObjetos)
        {
            VerificarPossibilidadeAlteracao();
            var atividade = this.Atividades.FirstOrDefault(r => r.PactoTrabalhoAtividadeId == pactoTrabalhoAtividadeId);
            atividade.Alterar(itemCatalogo.ItemCatalogoId, quantidade, modalidaExecucaoId, tempoPrevistoPorItem, descricao);
            if (idsAssuntos != null)
            {
                var idsAssuntosARemover = atividade.Assuntos.Select(a => a.AssuntoId).Where(id => !idsAssuntos.Contains(id)).ToList();
                var idsAssuntosAIncluir = idsAssuntos.Where(id => !atividade.Assuntos.Select(a => a.AssuntoId).Contains(id)).ToList();
                foreach (var id in idsAssuntosARemover) { atividade.RemoverAssunto(id); }
                foreach (var id in idsAssuntosAIncluir) { atividade.AdicionarAssunto(id); }
            }
            if (idsObjetos != null)
            {
                var idsObjetosARemover = atividade.Objetos.Select(a => a.PlanoTrabalhoObjetoId).Where(id => !idsObjetos.Contains(id)).ToList();
                var idsObjetosAIncluir = idsObjetos.Where(id => !atividade.Objetos.Select(a => a.PlanoTrabalhoObjetoId).Contains(id)).ToList();
                foreach (var id in idsObjetosARemover) { atividade.RemoverObjeto(id); }
                foreach (var id in idsObjetosAIncluir) { atividade.AdicionarObjeto(id); }
            }
            if (itemCatalogo.FormaCalculoTempoItemCatalogoId == (int)FormaCalculoTempoItemCatalogoEnum.PredefinidoPorDia)
                atividade.AtualizarTempoPrevistoTotal(WorkingDays.DiffDays(this.DataInicio, this.DataFim, this.DiasNaoUteis, false));
        }


        public void RemoverAtividade(Guid pactoTrabalhoAtividadeId)
        {
            VerificarPossibilidadeAlteracao();
            this.Atividades.RemoveAll(r => r.PactoTrabalhoAtividadeId == pactoTrabalhoAtividadeId);
        }

        #endregion

        #region Atualização de andamento

        /// <summary>
        /// Realiza a alteração manual de atividade
        /// </summary>
        /// <param name="pactoTrabalhoAtividadeId"></param>
        /// <param name="situacaoId"></param>
        /// <param name="dataInicio"></param>
        /// <param name="dataFim"></param>
        /// <param name="tempoRealizado"></param>
        public void AlterarAndamentoAtividade(Guid pactoTrabalhoAtividadeId, Int32 situacaoId, DateTime? dataInicio, DateTime? dataFim, Decimal? tempoRealizado, string consideracoes)
        {
            VerificarPossibilidadeAlteracao(SituacaoPactoTrabalhoEnum.EmExecucao);
            var atividade = this.Atividades.FirstOrDefault(r => r.PactoTrabalhoAtividadeId == pactoTrabalhoAtividadeId);

            atividade.AlterarAndamento(situacaoId, dataInicio, dataFim, tempoRealizado, consideracoes: consideracoes);

            this.AtualizarPercentualExecucao();
        }

        /// <summary>
        /// Realiza as operações do Kanban
        /// </summary>
        /// <param name="pactoTrabalhoAtividadeId"></param>
        /// <param name="situacaoId"></param>
        public void AlterarSituacaoAtividade(Guid pactoTrabalhoAtividadeId, Int32 situacaoId)
        {
            VerificarPossibilidadeAlteracao(SituacaoPactoTrabalhoEnum.EmExecucao);
            var atividade = this.Atividades.FirstOrDefault(r => r.PactoTrabalhoAtividadeId == pactoTrabalhoAtividadeId);

            //atividades por dia não podem ser controladas manualmente pelo servidor
            if (atividade.ItemCatalogo.FormaCalculoTempoItemCatalogoId == (int)FormaCalculoTempoItemCatalogoEnum.PredefinidoPorDia)
                return;

            switch (situacaoId)
            {
                case (int)SituacaoAtividadePactoTrabalhoEnum.ToDo:
                    atividade.DefinirComoPendente();
                    break;

                case (int)SituacaoAtividadePactoTrabalhoEnum.Doing:
                    atividade.DefinirComoEmExecucao();
                    break;

                case (int)SituacaoAtividadePactoTrabalhoEnum.Done:
                    atividade.DefinirComoConcluida(DateTime.Now, this.DiasNaoUteis, this.CargaHorariaDiaria);
                    break;
            }

            this.AtualizarPercentualExecucao();
        }

        private void AtualizarPercentualExecucao()
        {
            this.AtualizarTempoAtividadesPorDia();

            #region Atividades por tarefa

            //Recupera somente as atividades por tarefa
            var atividadesPorItem = this.Atividades.Where(a => a.ItemCatalogo == null || a.ItemCatalogo.FormaCalculoTempoItemCatalogoId == (int)Enums.FormaCalculoTempoItemCatalogoEnum.PredefinidoPorTarefa);
            var tempoPrevistoTotal = atividadesPorItem.Sum(a => a.TempoPrevistoPorItem);
            var tempoPrevistoAtividadesConcluidas = atividadesPorItem.Where(a => a.DataFim.HasValue).Sum(a => a.TempoPrevistoPorItem);

            //No tempo realizado considera as seguintes regras:
            //  1. Se o tempo tiver sido homologado e não tiver sido zero, considera o tempo homologado
            //  2. Se tiver sido homologado como zero, considera o tempo que estava previsto
            //  3. Se ainda não tiver sido homologado, considera o tempo realizado
            var tempoRealizadoAtividadesConcluidas = atividadesPorItem.Where(a => a.DataFim.HasValue).Sum(a => a.TempoHomologado.HasValue ? (a.TempoHomologado.Value > 0 ? a.TempoHomologado.Value : a.TempoPrevistoPorItem) : (a.TempoRealizado.HasValue ? a.TempoRealizado.Value : 0));

            #endregion

            #region Atividades por dia
            var atividadesPorDia = this.Atividades.Except(atividadesPorItem);

            //Calcula a quantidade total de dias do pacto
            var diasTotaisPacto = this.TempoTotalDisponivel / this.CargaHorariaDiaria;
            tempoPrevistoTotal += (diasTotaisPacto * atividadesPorDia.Sum(a => a.TempoPrevistoPorItem));

            //Se tiver alguma atividade concluída, usa a quantidade total de dias para calcular
            //  Caso contrário, usa o número de dias já executado
            var diasExecutadosPacto = diasTotaisPacto;
            if (this.Atividades.Any(a => !a.DataFim.HasValue))
            {//Para esse cálculo desconsidera os feriados
                diasExecutadosPacto = WorkingDays.DiffDays(this.DataInicio, DateTime.Now.Date > this.DataFim.Date ? this.DataFim.Date : DateTime.Now.Date, null, false);
            }

            tempoPrevistoAtividadesConcluidas += (diasExecutadosPacto * atividadesPorDia.Sum(a => a.TempoPrevistoPorItem));
            tempoRealizadoAtividadesConcluidas += (diasExecutadosPacto * atividadesPorDia.Sum(a => a.TempoHomologado.HasValue ? (a.TempoHomologado.Value > 0 ? a.TempoHomologado.Value : a.TempoPrevistoPorItem) : a.TempoPrevistoPorItem));

            #endregion

            this.PercentualExecucao = 0;
            if (tempoPrevistoTotal > 0)
                this.PercentualExecucao = Decimal.Round(Decimal.Divide(tempoPrevistoAtividadesConcluidas, tempoPrevistoTotal) * 100, 2);

            this.RelacaoPrevistoRealizado = null;
            if (tempoRealizadoAtividadesConcluidas > 0)
                this.RelacaoPrevistoRealizado = Decimal.Round(Decimal.Divide(tempoPrevistoAtividadesConcluidas, tempoRealizadoAtividadesConcluidas) * 100, 2);

        }

        private void AtualizarTempoAtividadesPorDia()
        {
            var dataFim = DateTime.Now.Date < this.DataFim ? DateTime.Now.Date : this.DataFim;
            var diasExecutados = WorkingDays.DiffDays(this.DataInicio, dataFim, this.DiasNaoUteis, false);
            var atividadesPorDia = this.Atividades.Where(a => a.ItemCatalogo != null && a.ItemCatalogo.FormaCalculoTempoItemCatalogoId == (int)FormaCalculoTempoItemCatalogoEnum.PredefinidoPorDia).ToList();
            atividadesPorDia.ForEach(a => a.AlterarTempoRealizado(diasExecutados * a.TempoPrevistoPorItem));
        }

        #endregion

        public void AvaliarAtividade(Guid pactoTrabalhoAtividadeId, int nota, string justificativa, string responsavel)
        {
            var atividade = this.Atividades.FirstOrDefault(r => r.PactoTrabalhoAtividadeId == pactoTrabalhoAtividadeId);
            atividade.Avaliar(nota, justificativa, responsavel);
            this.AtualizarPercentualExecucao();
        }

        #endregion

        #region Solicitações

        public void AdicionarSolicitacao(TipoSolicitacaoPactoTrabalhoEnum tipoSolicitacao, string solicitante, string dadosSolicitacao, string observacoesSolicitante)
        {
            VerificarPossibilidadeAlteracao(SituacaoPactoTrabalhoEnum.EmExecucao);
            var item = PactoTrabalhoSolicitacao.Criar(this.PactoTrabalhoId, (int)tipoSolicitacao, solicitante, dadosSolicitacao, observacoesSolicitante);
            this.Solicitacoes.Add(item);
        }

        public Boolean ExisteSolicitacaoPendenteExclusaoMesmaAtividade(Guid pactoTrabalhoAtividadeId)
        {
            //Obtém solicitações de exclusão pendentes
            var solicitacoesMesmoTipoPendentes = this.ObterSolicitacoesNaoAtendidasMesmoTipo(TipoSolicitacaoPactoTrabalhoEnum.ExcluirAtividade);

            //Deve rejeitar outras solicitações de exclusão da mesma atividade
            foreach (var solicitacaoVerificar in solicitacoesMesmoTipoPendentes)
            {
                dynamic dadosSolicitacaoVerificar = JsonConvert.DeserializeObject(solicitacaoVerificar.DadosSolicitacao);
                if (dadosSolicitacaoVerificar.pactoTrabalhoAtividadeId == pactoTrabalhoAtividadeId)
                {
                    return true;
                }
            }
            return false;
        }

        public void ResponderSolicitacao(Guid pactoTrabalhoSolicitacaoId, string analista, Boolean aprovado, Boolean atualizarPrazo, string observacoesAnalista, ItemCatalogo itemCatalogo)
        {
            VerificarPossibilidadeAlteracao(SituacaoPactoTrabalhoEnum.EmExecucao);
            var item = this.Solicitacoes.FirstOrDefault(r => r.PactoTrabalhoSolicitacaoId == pactoTrabalhoSolicitacaoId);

            if (aprovado)
            {
                dynamic dadosSolicitacao = JsonConvert.DeserializeObject(item.DadosSolicitacao);

                switch (item.TipoSolicitacaoId)
                {
                    case (int)TipoSolicitacaoPactoTrabalhoEnum.NovaAtividade:
                        Guid itemCatalogoId = dadosSolicitacao.itemCatalogoId;
                        Int32 quantidade = 1;
                        Decimal tempoPrevistoPorItem = dadosSolicitacao.tempoPrevistoPorItem;
                        Int32 situacaoId = dadosSolicitacao.situacaoId;
                        DateTime? dataInicio = null;
                        if (dadosSolicitacao.dataInicio != null) dataInicio = dadosSolicitacao.dataInicio;
                        DateTime? dataFim = null;
                        if (dadosSolicitacao.dataFim != null) dataFim = dadosSolicitacao.dataFim;
                        Decimal? tempoRealizado = null;
                        if (dadosSolicitacao.tempoRealizado != null) tempoRealizado = dadosSolicitacao.tempoRealizado;

                        Int32 modalidadeExecucaoId = this.ModalidadeExecucaoId;
                        if (this.ModalidadeExecucaoId == (int)ModalidadeExecucaoEnum.Semipresencial && dadosSolicitacao.execucaoRemota != null)
                        {
                            Boolean execucaoRemota = dadosSolicitacao.execucaoRemota;
                            modalidadeExecucaoId = execucaoRemota ? (int)ModalidadeExecucaoEnum.Teletrabalho : (int)ModalidadeExecucaoEnum.Presencial;
                        }
                        this.AprovarNovaAtividade(itemCatalogo, quantidade, modalidadeExecucaoId, tempoPrevistoPorItem, situacaoId, dataInicio, dataFim, tempoRealizado, item.ObservacoesSolicitante, atualizarPrazo);

                        break;

                    case (int)TipoSolicitacaoPactoTrabalhoEnum.AlterarPrazo:

                        this.AlterarPeriodo((DateTime)dadosSolicitacao.dataFim);
                        break;


                    case (int)TipoSolicitacaoPactoTrabalhoEnum.JustificarEstouroPrazo:

                        Guid? pactoTrabalhoAtividadeId = dadosSolicitacao.pactoTrabalhoAtividadeId;
                        var atividade = this.Atividades
                            .FirstOrDefault(a => a.PactoTrabalhoAtividadeId == pactoTrabalhoAtividadeId);

                        if (atividade.SituacaoId == (int)SituacaoAtividadePactoTrabalhoEnum.Done)
                        {
                            if (atualizarPrazo)
                            {
                                var novaDataFim = atividade.CalcularAjusteNoPrazo(this.DataFim, DiasNaoUteis);
                                this.AlterarPeriodo(novaDataFim);
                            }

                            atividade.AjustarTemposPrevistoEHomologadoAoTempoRealizado();
                            this.AtualizarPercentualExecucao();
                        }

                        break;

                    case (int)TipoSolicitacaoPactoTrabalhoEnum.ExcluirAtividade:
                        Guid pactoTrabalhoAtividadeExcluirId = dadosSolicitacao.pactoTrabalhoAtividadeId;
                        this.Atividades.RemoveAll(a => a.PactoTrabalhoAtividadeId == pactoTrabalhoAtividadeExcluirId);

                        //Obtém solicitações de exclusão pendentes
                        var solicitacoesMesmoTipoPendentes = this.ObterSolicitacoesNaoAtendidasMesmoTipo(TipoSolicitacaoPactoTrabalhoEnum.ExcluirAtividade, pactoTrabalhoSolicitacaoId);

                        //Deve rejeitar outras solicitações de exclusão da mesma atividade
                        foreach (var solicitacaoVerificar in solicitacoesMesmoTipoPendentes)
                        {
                            dynamic dadosSolicitacaoVerificar = JsonConvert.DeserializeObject(solicitacaoVerificar.DadosSolicitacao);
                            if (dadosSolicitacaoVerificar.pactoTrabalhoAtividadeId == dadosSolicitacao.pactoTrabalhoAtividadeId)
                            {
                                solicitacaoVerificar.Responder(analista, false, "Uma outra solicitação de exclusão da mesma atividade foi aprovada");
                            }
                        }
                        break;
                }
            }

            item.Responder(analista, aprovado, observacoesAnalista);
        }

        public List<PactoTrabalhoSolicitacao> ObterSolicitacoesNaoAtendidasMesmoTipo(TipoSolicitacaoPactoTrabalhoEnum tipoSolicitacao, Guid? pactoTrabalhoSolicitacaoId = null)
        {
            //Deve rejeitar outras solicitações de exclusão da mesma atividade
            return this.Solicitacoes.Where(s => !s.Analisado &&
                                                s.TipoSolicitacaoId == (int)tipoSolicitacao &&
                                                (!pactoTrabalhoSolicitacaoId.HasValue || s.PactoTrabalhoSolicitacaoId != pactoTrabalhoSolicitacaoId))
                                    .ToList();
        }

        public void AprovarNovaAtividade(ItemCatalogo itemCatalogo, int quantidade, int modalidaExecucaoId, decimal tempoPrevistoPorItem, Int32 situacaoId, DateTime? dataInicio, DateTime? dataFim, Decimal? tempoRealizado, string descricao, bool atualizarPrazo)
        {
            var atividade = PactoTrabalhoAtividade.Criar(this, itemCatalogo, quantidade, modalidaExecucaoId, tempoPrevistoPorItem, descricao);
            atividade.AlterarAndamento(situacaoId, dataInicio, dataFim, tempoRealizado, ignorarValidacoes: true);
            this.Atividades.Add(atividade);

            if (itemCatalogo.FormaCalculoTempoItemCatalogoId == (int)FormaCalculoTempoItemCatalogoEnum.PredefinidoPorDia)
                atividade.AtualizarTempoPrevistoTotal(WorkingDays.DiffDays(this.DataInicio, this.DataFim, this.DiasNaoUteis, false));

            if (atualizarPrazo)
            {
                var novaDataFim = atividade.CalcularAjusteNoPrazo(this.DataFim, DiasNaoUteis, tempoPrevistoPorItem);
                this.AlterarPeriodo(novaDataFim);
            }

            this.AtualizarPercentualExecucao();
        }

        #endregion

        private void VerificarPossibilidadeAlteracao(SituacaoPactoTrabalhoEnum situacao = SituacaoPactoTrabalhoEnum.Rascunho)
        {
            if (SituacaoId != (int)situacao)
                throw new SISRHDomainException("A situação atual do plano não permite realizar esta operação");
        }

        private void ValidarDatas(DateTime dataInicio, DateTime dataFim)
        {

            if (DataInicio != dataInicio && dataInicio < DateTime.Now.Date)
                throw new SISRHDomainException("A data de início do plano de trabalho deve ser maior ou igual à data atual");

            if (this.SituacaoId == (int)Enums.SituacaoPactoTrabalhoEnum.Rascunho && dataFim < DateTime.Now.Date)
                throw new SISRHDomainException("A data de fim do plano de trabalho deve ser maior ou igual à data atual");

            if (dataFim <= dataInicio)
                throw new SISRHDomainException("A data de fim do plano de trabalho deve ser maior que a data de início");

        }

        public void RegistrarVisualizacaoDeclaracao(int declaracaoId, string responsavelRegistro)
        {
            if (!this.Declaracoes.Any(it => it.DeclaracaoId == declaracaoId))
            {
                var declaracao = PactoTrabalhoDeclaracao.Criar(declaracaoId, responsavelRegistro);
                this.Declaracoes.Add(declaracao);
            }
        }

        public void RegistrarRealizacaoDeclaracao(int declaracaoId, string responsavelRegistro)
        {
            var declaracao = this.Declaracoes.FirstOrDefault(it => it.DeclaracaoId == declaracaoId);
            declaracao.RegistrarAceite(responsavelRegistro);
        }

        public PactoTrabalhoInformacao RegistrarInformacao(string texto, string responsavelRegistro)
        {
            var informacao = PactoTrabalhoInformacao.Criar(texto, responsavelRegistro);
            this.Informacoes.Add(informacao);
            return informacao;
        }


    }
}
