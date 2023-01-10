﻿namespace Susep.SISRH.Application.Queries.RawSql
{
    public static class UnidadeRawSqls
    {

        public static string ObterAtivas
        {
            get
            {
                return @"
					SELECT DISTINCT u.unidadeId as Id
	                                ,u.undSiglaCompleta as Descricao   
                                    ,u.unidadeId
	                                ,u.undSiglaCompleta siglaCompleta
                                    ,u.unidadeIdPai     
                    FROM [dbo].[VW_UnidadeSiglaCompleta] u
                    WHERE situacaoUnidadeId = @situacaoAtiva
                    ORDER BY u.undSiglaCompleta
                ";
            }
        }

        public static string ObterComPlanoTrabalho
        {
            get
            {
                return @"
					SELECT DISTINCT u.unidadeId as Id
	                                ,u.undSiglaCompleta as Descricao   
                                    ,u.unidadeId
	                                ,u.undSiglaCompleta siglaCompleta
                                    ,u.unidadeIdPai   
                    FROM [dbo].[VW_UnidadeSiglaCompleta] u
                        INNER JOIN [ProgramaGestao].[PlanoTrabalho] p ON u.unidadeId = p.unidadeId                    
                    ORDER BY u.undSiglaCompleta
                ";
            }
        }
        
        public static string ObterSemCatalogoCadastrado
        {
            get
            {
                return @"
					SELECT DISTINCT u.unidadeId as Id
	                                ,u.undSiglaCompleta as Descricao   
                                    ,u.unidadeId
	                                ,u.undSiglaCompleta siglaCompleta
                                    ,u.unidadeIdPai         
                    FROM [dbo].[VW_UnidadeSiglaCompleta] u
                        LEFT OUTER JOIN [ProgramaGestao].[Catalogo] c ON u.unidadeId = c.unidadeId  
                    WHERE c.unidadeId IS NULL AND u.[situacaoUnidadeId] = 1
                    ORDER BY u.undSiglaCompleta
                ";
            }
        }

        public static string ObterComCatalogoCadastrado
        {
            get
            {
                return @"
					SELECT DISTINCT u.unidadeId as Id
	                                ,u.undSiglaCompleta as Descricao   
                                    ,u.unidadeId
	                                ,u.undSiglaCompleta siglaCompleta
                                    ,u.unidadeIdPai        
                    FROM [dbo].[VW_UnidadeSiglaCompleta] u
                        LEFT OUTER JOIN [ProgramaGestao].[Catalogo] c ON u.unidadeId = c.unidadeId  
                    WHERE c.unidadeId IS NOT NULL AND u.situacaoUnidadeId = 1
                    ORDER BY u.undSiglaCompleta
                ";
            }
        }

        public static string ObterPorChave
        {
            get
            {
                return @"
                    SELECT  u.[unidadeId]
                            ,[undSigla]
                            ,[undDescricao] nome
                            ,[unidadeIdPai]
                            ,[tipoUnidadeId]
                            ,[situacaoUnidadeId]
                            ,[ufId]
                            ,[undNivel] Nivel
                            ,[tipoFuncaoUnidadeId]
                            ,[undSiglaCompleta] siglaCompleta
                            ,[Email]
                    FROM [dbo].[VW_UnidadeSiglaCompleta] u  
                    WHERE u.[unidadeId] = @unidadeId
                ";
            }
        }


        public static string ObterQuantidadeServidoresIncluirSubunidadesPorChave
        {
            get
            {
                return @"
                    SELECT  upg.[unidadeId]
                        ,[undSigla]
                        ,[undDescricao] nome
                        ,[unidadeIdPai]
                        ,[tipoUnidadeId]
                        ,[situacaoUnidadeId]
                        ,[ufId]
                        ,[undNivel] Nivel
                        ,[tipoFuncaoUnidadeId]
                        ,[undSiglaCompleta] siglaCompleta
                        ,[Email]
                        ,quantidadeServidores
                    FROM [dbo].[VW_UnidadeSiglaCompleta] vu
                            INNER JOIN (
                                        SELECT up.[unidadeId], count(1) quantidadeServidores
                                        FROM (SELECT p.pessoaid, p.[unidadeId]
                                              FROM [dbo].[Pessoa] p
                                              WHERE p.situacaoPessoaId = 1 AND p.tipoVinculoId not in (5, 6) 

                                              UNION 

                                              SELECT p.pessoaid, pat.unidadeId
                                              FROM PessoaAlocacaoTemporaria pat
                                                  INNER JOIN [dbo].[Pessoa] p ON p.pessoaId = pat.pessoaId
                                              WHERE p.situacaoPessoaId = 1 AND p.tipoVinculoId not in (5, 6) 
                                                  AND (pat.dataInicio <= getdate()) AND (pat.dataFim IS NULL OR pat.dataFim <= getdate())
                                        ) up 
                                        WHERE up.unidadeId IN (SELECT unidadeId
                                                               FROM [dbo].[VW_UnidadeSiglaCompleta]
                                                               WHERE undSiglaCompleta like (SELECT u.undSiglaCompleta + '%'
                                                                                            FROM [dbo].[VW_UnidadeSiglaCompleta] u
                                                                                             WHERE u.[unidadeId] = @unidadeId))
                                        GROUP BY up.[unidadeId]) upg ON vu.unidadeId = upg.unidadeId
                ";
            }
        }        

        public static string ObterFeriados
        {
            get
            {
                return @"
                    SELECT  DATEFROMPARTS ( YEAR(@dataInicio), MONTH(ferData), DAY(ferData) ) data
		                    ,ferFixo fixo
		                    ,f.ufId ufId 
                    FROM [dbo].[Feriado] f
                    WHERE ((f.ferFixo = 1
		                    AND DATEFROMPARTS ( YEAR(@dataInicio), MONTH(ferData), DAY(ferData) ) >= DATEFROMPARTS ( YEAR(@dataInicio), MONTH(@dataInicio), DAY(@dataInicio) ) 
		                    AND DATEFROMPARTS ( YEAR(@dataFim), MONTH(ferData), DAY(ferData) ) <= DATEFROMPARTS ( YEAR(@dataFim), MONTH(@dataFim), DAY(@dataFim) ))
	                       OR 
	                       (f.ferFixo = 0 AND ferData >= @dataInicio AND ferData <= @dataFim))					  
	                    AND (f.ufId IS NULL 
		                     OR f.ufId = (SELECT ufId 
					                      FROM [dbo].[Unidade] 
					                      WHERE unidadeId = @unidadeId))
                ";
            }
        }

        public static string ObterModalidadesExecucaoPorUnidade
        {
            get
            {
                return @"
                    SELECT catalogoDominioId as id, descricao
                    FROM [ProgramaGestao].[UnidadeModalidadeExecucao] ue
                        INNER JOIN [dbo].[CatalogoDominio] cd ON cd.catalogoDominioId = ue.modalidadeExecucaoId
                    WHERE unidadeId = @unidadeId
                    ORDER BY descricao
                ";
            }
        }

        public static string ObterPessoasDadosComboPorUnidade
        {
            get
            {
                return @"
                    SELECT p.pessoaId as id, pesNome descricao
                    FROM [dbo].[Pessoa] p
			            LEFT OUTER JOIN [dbo].[PessoaAlocacaoTemporaria] a ON a.pessoaId = p.pessoaId AND a.dataFim IS NULL
	                    INNER JOIN [dbo].[VW_UnidadeSiglaCompleta] u ON u.unidadeId = COALESCE(a.unidadeId, p.unidadeId)
                    WHERE ((COALESCE(a.unidadeId, p.unidadeId) = @unidadeId) OR (u.unidadeIdPai = @unidadeId and p.tipoFuncaoId IS NOT NULL))
                        AND p.situacaoPessoaId = 1 AND p.tipoVinculoId NOT IN (5,6)
                    ORDER BY pesNome
                ";
            }
        }

        public static string ObterPessoasPorUnidade
        {
            get
            {
                return @"
                    SELECT *
                    FROM (SELECT p.pessoaid, pesNome nome, pesEmail email, tipoFuncaoId, p.[unidadeId], IIF(u1.unidadeId IS NOT NULL, 1, 0) chefe
                            FROM [dbo].[Pessoa] p
                                LEFT OUTER JOIN [dbo].[Unidade] u1 ON u1.unidadeId = p.unidadeId AND (u1.pessoaIdChefe = p.pessoaId OR u1.pessoaIdChefeSubstituto = p.pessoaId)
                            WHERE p.situacaoPessoaId = 1 AND p.tipoVinculoId not in (5, 6) 

                            UNION 

                            SELECT p.pessoaid, pesNome nome, pesEmail email, tipoFuncaoId, pat.unidadeId, IIF(u1.unidadeId IS NOT NULL, 1, 0) chefe
                            FROM PessoaAlocacaoTemporaria pat
                                INNER JOIN [dbo].[Pessoa] p ON p.pessoaId = pat.pessoaId
                                LEFT OUTER JOIN [dbo].[Unidade] u1 ON u1.unidadeId = p.unidadeId AND (u1.pessoaIdChefe = p.pessoaId OR u1.pessoaIdChefeSubstituto = p.pessoaId)
                            WHERE p.situacaoPessoaId = 1 AND p.tipoVinculoId not in (5, 6)  
                                AND (pat.dataInicio <= getdate()) AND (pat.dataFim IS NULL OR pat.dataFim <= getdate())
                    ) up 
                    WHERE up.unidadeId IN (SELECT unidadeId
                                            FROM [dbo].[VW_UnidadeSiglaCompleta]
                                            WHERE undSiglaCompleta like (SELECT u.undSiglaCompleta + '%'
                                                                        FROM [dbo].[VW_UnidadeSiglaCompleta] u
                                                                        WHERE u.[unidadeId] = @unidadeId))
                    ORDER BY up.nome
                ";
            }
        }

        public static string ObterPessoasDiretamenteAlocadasPorUnidade
        {
            get
            {
                return @"
                    SELECT p.pessoaid, pesNome nome, pesEmail email, tipoFuncaoId, p.[unidadeId], CASE WHEN (u1.pessoaIdChefe = p.pessoaId OR u1.pessoaIdChefeSubstituto = p.pessoaId) THEN 1 ELSE 0 END chefe
                    FROM [dbo].[Pessoa] p
                        LEFT OUTER JOIN [dbo].[Unidade] u1 ON u1.unidadeId = p.unidadeId 
                    WHERE p.situacaoPessoaId = 1 AND p.tipoVinculoId not in (5, 6)
                        AND u1.unidadeId = @unidadeId

                    UNION 

                    SELECT p.pessoaid, pesNome nome, pesEmail email, tipoFuncaoId, pat.unidadeId, IIF(u1.unidadeId IS NOT NULL, 1, 0) chefe
                    FROM PessoaAlocacaoTemporaria pat
                        INNER JOIN [dbo].[Pessoa] p ON p.pessoaId = pat.pessoaId
                        LEFT OUTER JOIN [dbo].[Unidade] u1 ON u1.unidadeId = p.unidadeId AND (u1.pessoaIdChefe = p.pessoaId OR u1.pessoaIdChefeSubstituto = p.pessoaId)
                    WHERE p.situacaoPessoaId = 1 AND p.tipoVinculoId not in (5, 6)
                        AND (pat.dataInicio <= getdate()) AND (pat.dataFim IS NULL OR pat.dataFim <= getdate())
                        AND pat.unidadeId = @unidadeId
              
                    UNION 

                    SELECT p.pessoaid, pesNome nome, pesEmail email, tipoFuncaoId, upai.unidadeId, 0 chefe
                    FROM [dbo].[Unidade] upai
                    INNER JOIN [dbo].[Unidade] ufilha ON upai.unidadeId = ufilha.unidadeIdPai
                    INNER JOIN [dbo].[Pessoa] p ON p.pessoaId = ufilha.pessoaIdChefe
                    WHERE p.situacaoPessoaId = 1 AND ufilha.situacaoUnidadeId = 1 AND p.tipoVinculoId not in (5, 6)
                        AND upai.unidadeId = @unidadeId
                                                
                    UNION 

                    SELECT p.pessoaid, pesNome nome, pesEmail email, tipoFuncaoId, upai.unidadeId, 0 chefe
                    FROM [dbo].[Unidade] upai
                    INNER JOIN [dbo].[Unidade] ufilha ON upai.unidadeId = ufilha.unidadeIdPai
                    INNER JOIN [dbo].[Pessoa] p ON p.pessoaId = ufilha.pessoaIdChefeSubstituto
                    WHERE p.situacaoPessoaId = 1 AND ufilha.situacaoUnidadeId = 1 AND p.tipoVinculoId not in (5, 6) 
                        AND upai.unidadeId = @unidadeId
                ";
            }
        }

        public static string ObterChefesPorUnidade
        {
            get
            {
                return @"
                    SELECT pessoaId pessoaId, pesNome nome, pesEmail email
                    FROM [dbo].[Pessoa] p
	                    INNER JOIN [dbo].[VW_UnidadeSiglaCompleta] u ON p.unidadeId = u.unidadeId
                    WHERE p.tipoFuncaoId IS NOT NULL
	                    and u.undSiglaCompleta IN @siglas
                ";
            }
        }

        public static string ObterSubordinadasPorUnidade
        {
            get
            {
                return @"
                    SELECT unidadeId id
	                       ,undSiglaCompleta descricao
                    FROM [dbo].[VW_UnidadeSiglaCompleta] u
                        WHERE undSiglaCompleta LIKE (SELECT undSiglaCompleta + '%' FROM [dbo].[VW_UnidadeSiglaCompleta] us WHERE us.unidadeId = @unidadeId)
                    ORDER BY undSiglaCompleta
                ";
            }
        }

        public static string ObterPorChefe
        {
            get
            {
                return @"
                    SELECT u.unidadeId
                           ,usc.undSiglaCompleta unidade
                           ,u.undNivel nivelUnidade
                           ,u.tipoFuncaoUnidadeId
                           ,u.UnidadeIdPai
                    FROM [dbo].[Unidade] u
						INNER JOIN VW_UnidadeSiglaCompleta usc ON u.unidadeId = usc.unidadeId
                        WHERE (u.pessoaIdChefe = @pessoaId OR u.pessoaIdChefeSubstituto = @pessoaId)
                    ORDER BY undSiglaCompleta
                ";
            }
        }



        public static string EstruturaAtual
        {
            get
            {
                return @"
                    SELECT DISTINCT u.unidadeId as Id
	                                ,u.undSiglaCompleta as Descricao   
                                    ,u.unidadeId
	                                ,u.undSiglaCompleta siglaCompleta
                                    ,u.unidadeIdPai     
                    FROM [dbo].[VW_UnidadeSiglaCompleta] u
                    WHERE situacaoUnidadeId = @situacaoUnidadeAtiva
                    ORDER BY u.undSiglaCompleta

                    SELECT DISTINCT 
                           p.pessoaId
                          ,UPPER(RTRIM(LTRIM(p.pesNome))) nome
                          ,p.unidadeId
                          ,u.undSiglaCompleta unidade
                          ,sp.situacaoPessoaId
                          ,sp.spsDescricao situacaoPessoa
                          ,tv.tipoVinculoId
                          ,tv.tvnDescricao tipoVinculo
                          ,p.CargaHoraria
                    FROM [dbo].[Pessoa] p
                        LEFT OUTER JOIN [dbo].[PessoaAlocacaoTemporaria] pat ON p.pessoaId = pat.pessoaId AND (dataFim IS NULL OR dataFim > GETDATE())
                        INNER JOIN [dbo].[VW_UnidadeSiglaCompleta] u ON u.unidadeId = ISNULL(pat.unidadeId, p.unidadeId)   
					    INNER JOIN [dbo].[situacaoPessoa] sp ON sp.situacaoPessoaId = p.situacaoPessoaId
					    INNER JOIN [dbo].[TipoVinculo] tv ON tv.tipoVinculoId = p.tipoVinculoId
                    WHERE   p.situacaoPessoaId = 1
                    ORDER BY nome ASC, unidadeId DESC, CargaHoraria ASC
                ";
            }
        }
        

    }
}
