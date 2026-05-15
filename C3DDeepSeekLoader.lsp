;; C3DDeepSeekLoader.lsp
;; Auto-carrega o plugin DeepSeek no Civil 3D / AutoCAD 2026
;; Coloque este arquivo na pasta Support do AutoCAD ou use APPLOAD

(defun C:C3DDSLOAD ()
  (setq dll-path "C:\\Users\\paulo.lima\\OneDrive - ATERPA\\00. PERSONALIZADOS\\AUTODESK\\PERSONALIZADOS\\DEEPSEEK\\C3DDeepSeek.bundle\\Contents\\C3DDeepSeek.dll")
  (if (findfile dll-path)
    (progn
      (command "_.NETLOAD" dll-path)
      (princ "\n[C3D DeepSeek] Plugin carregado com sucesso! Use DEEPSEEK, DSASK ou CONFIGDS.")
    )
    (princ "\n[C3D DeepSeek] ERRO: DLL nao encontrada. Execute NETLOAD manualmente.")
  )
  (princ)
)

;; Carrega automaticamente ao iniciar
(C:C3DDSLOAD)
