; ╔══════════════════════════════════════════════════════════════════════╗
; ║       L U M I N A   L E X  — AI Language Engine  v3.6              ║
; ╚══════════════════════════════════════════════════════════════════════╝
;
; ── Configuration des clés API ──────────────────────────────────────────────
; Créez un fichier  config.ini  dans le MÊME dossier que ce script.
; Contenu attendu :
;
;   [API]
;   OPENAI_KEY=sk-proj-...
;   DEEPL_KEY=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx:fx   ; :fx = endpoint Free
;
; Les variables d'environnement OPENAI_API_KEY / DEEPL_API_KEY sont utilisées
; en fallback si config.ini est absent ou incomplet.
;
; ── Timeouts configurables (ms) — variables d'environnement ─────────────────
;   LUMINALEX_TIMEOUT_RESOLVE   (défaut : 5000)
;   LUMINALEX_TIMEOUT_CONNECT   (défaut : 10000)
;   LUMINALEX_TIMEOUT_SEND      (défaut : 0  — illimité)
;   LUMINALEX_TIMEOUT_RECEIVE   (défaut : 30000)
; ─────────────────────────────────────────────────────────────────────────────
#Requires AutoHotkey v2.0
#SingleInstance Force
Persistent
SetWinDelay(-1)
SetControlDelay(-1)
SetWorkingDir(A_ScriptDir)

class LuminaLexApp {
    __New() {
        this.visible := false
        this.width := 1220
        this.height := 980
        this.BuildGui()
        this.pipeline := PipelineManager(this)
        this.ShowGuiInitial()
        this.SetStatus("Prêt", 0)
    }

    BuildGui() {
        this.gui := Gui("-Caption +ToolWindow +AlwaysOnTop +Resize +MinSize700x400", "Lumina Lex")
        this.gui.BackColor := "030B1A"
        this.gui.MarginX := 0
        this.gui.MarginY := 0
        this.gui.SetFont("s10.5 cEDF2FF", "Segoe UI Variable Text")
        this.bgBase := this.gui.AddText("Background030B1A x0 y0 w2000 h2000", "")
        this.headerLineL := this.gui.AddText("Background4F46E5 x22 y22 w600 h1", "")
        this.headerLineR := this.gui.AddText("BackgroundDB2777 x622 y22 w600 h1", "")
        this.lblTitleIcon := this.gui.AddText("c8B5CF6 BackgroundTrans x44 y38", "⬢")
        this.lblTitleIcon.SetFont("s19 w700", "Segoe UI Symbol")
        this.lblTitle := this.gui.AddText("cEDF2FF BackgroundTrans x78 y34", "Lumina Lex")
        this.lblTitle.SetFont("s22 w700", "Segoe UI Variable Display Semib")
        this.lblTitleSub := this.gui.AddText("cA8B7FF BackgroundTrans x78 y70", "Moteur linguistique premium · Responses API · DeepL v2")
        this.btnOverlayClose := this.gui.AddText("cF07BFF BackgroundTrans x1160 y34 w30 h30 Center", "✕")
        this.btnOverlayClose.SetFont("s14 w700")
        this.btnOverlayClose.OnEvent("Click", (*) => ExitApp())
        this.lblInput := this.gui.AddText("cEDF2FF BackgroundTrans x48 y130", "Texte source")
        this.inEdit := this.gui.AddEdit("WantTab Multi -Border Background050C1A cEDF2FF x44 y160 w1132 h250")
        this.btnCorrect := this.gui.AddText("Center 0x200 Background4F46E5 cEDF2FF x44 y430 w450 h44", "Corriger (Turbo)")
        this.btnTranslate := this.gui.AddText("Center 0x200 BackgroundDB2777 cEDF2FF x510 y430 w450 h44", "Traduire")
        this.btnClear := this.gui.AddText("Center 0x200 Background1E274D cEDF2FF x976 y430 w200 h44", "Effacer")
        this.lblOutput := this.gui.AddText("cEDF2FF BackgroundTrans x48 y500", "Résultat")
        this.outEdit := this.gui.AddEdit("ReadOnly WantTab Multi -Border Background050C1A cEDF2FF x44 y530 w1132 h250")
        this.txtStatusBody := this.gui.AddText("c818CF8 BackgroundTrans x48 y820 w1100 h20", "Initialisation...")
        this.btnCorrect.OnEvent("Click", (*) => this.pipeline.StartTask(this.inEdit.Value, "Correction"))
        this.btnTranslate.OnEvent("Click", (*) => this.pipeline.StartTask(this.inEdit.Value, "Combined_Correction"))
        this.btnClear.OnEvent("Click", (*) => this.ClearAll())
        this.ApplyWindowEffects()
        OnMessage(0x0201, ObjBindMethod(this, "OnDrag"))
    }

    ApplyWindowEffects() {
        hwnd := this.gui.Hwnd
        try {
            DllCall("dwmapi\DwmSetWindowAttribute", "ptr", hwnd, "int", 20, "int*", 1, "int", 4)
            DllCall("dwmapi\DwmSetWindowAttribute", "ptr", hwnd, "int", 33, "int*", 2, "int", 4)
            DllCall("dwmapi\DwmSetWindowAttribute", "ptr", hwnd, "int", 38, "int*", 2, "int", 4)
        }
    }

    ShowGuiInitial() {
        this.gui.Show("w" this.width " h" this.height " Hide")
        this.OverlaySetAlpha(this.gui.Hwnd, 0)
    }

    OverlaySetAlpha(hwnd, alpha) {
        exStyle := DllCall("GetWindowLong", "ptr", hwnd, "int", -20, "uint")
        DllCall("SetWindowLong", "ptr", hwnd, "int", -20, "uint", exStyle | 0x80000)
        DllCall("SetLayeredWindowAttributes", "ptr", hwnd, "uint", 0, "uchar", alpha, "uint", 2)
    }

    ToggleGui() {
        if (this.visible) {
            this.gui.Hide()
            this.visible := false
        } else {
            this.gui.Show("NoActivate")
            this.OverlaySetAlpha(this.gui.Hwnd, 240)
            WinActivate("ahk_id " this.gui.Hwnd)
            this.visible := true
        }
    }

    ClearAll() {
        this.inEdit.Value := ""
        this.outEdit.Value := ""
        this.SetStatus("Effacé.", 0)
    }

    SetStatus(text, progress := 0) {
        this.txtStatusBody.Text := text
    }

    OnDrag(wParam, lParam, msg, hwnd) {
        if (hwnd = this.gui.Hwnd || hwnd = this.bgBase.Hwnd) {
            DllCall("ReleaseCapture")
            PostMessage(0x00A1, 2,,, "ahk_id " this.gui.Hwnd)
        }
    }
}

; ─────────────────────────────────────────────────────────────────────────────
; PipelineManager
;
; Priorité de lecture des clés :
;   1. config.ini [API] OPENAI_KEY / DEEPL_KEY   (recommandé)
;   2. Variables d'environnement OPENAI_API_KEY / DEEPL_API_KEY  (fallback)
;
; Endpoint DeepL sélectionné automatiquement :
;   clé contenant ":fx"  →  api-free.deepl.com   (Free)
;   sinon                →  api.deepl.com         (Pro)
; ─────────────────────────────────────────────────────────────────────────────
class PipelineManager {
    ; OpenAI Responses API — seul endpoint utilisé pour la correction
    static OPENAI_URL := "https://api.openai.com/v1/responses"
    static INI_FILE   := A_ScriptDir "\config.ini"

    __New(app) {
        this.app := app
        this.http := ComObject("Msxml2.ServerXMLHTTP.6.0")
        this.timerFn := ObjBindMethod(this, "CheckAsyncState")
        this.isProcessing := false

        ; ── Lecture des clés (config.ini prioritaire, env en fallback) ────────
        this.openaiKey := this.LoadKey("OPENAI_KEY", "OPENAI_API_KEY")
        this.deeplKey  := this.LoadKey("DEEPL_KEY",  "DEEPL_API_KEY")

        ; ── Sélection automatique endpoint DeepL Free / Pro ──────────────────
        this.deeplUrl := InStr(this.deeplKey, ":fx")
            ? "https://api-free.deepl.com/v2/translate"
            : "https://api.deepl.com/v2/translate"

        ; ── Timeouts configurables par variables d'environnement (ms) ─────────
        this.timeouts := [
            this.LoadTimeout("LUMINALEX_TIMEOUT_RESOLVE",  5000),
            this.LoadTimeout("LUMINALEX_TIMEOUT_CONNECT",  10000),
            this.LoadTimeout("LUMINALEX_TIMEOUT_SEND",     0),
            this.LoadTimeout("LUMINALEX_TIMEOUT_RECEIVE",  30000)
        ]

        ; ── Statut au démarrage avec aperçu masqué des clés ──────────────────
        if (this.openaiKey = "" && this.deeplKey = "") {
            this.app.SetStatus("⚠ Aucune clé trouvée — créez config.ini (voir en-tête du script)", 0)
        } else if (this.openaiKey = "") {
            this.app.SetStatus("⚠ Clé OpenAI manquante dans config.ini (OPENAI_KEY)", 0)
        } else if (this.deeplKey = "") {
            this.app.SetStatus("⚠ Clé DeepL manquante dans config.ini (DEEPL_KEY)", 0)
        } else {
            oaiMask := SubStr(this.openaiKey, 1, 8) "..." SubStr(this.openaiKey, -3)
            dplMask := SubStr(this.deeplKey,  1, 8) "..." SubStr(this.deeplKey,  -3)
            endpoint := InStr(this.deeplKey, ":fx") ? "Free" : "Pro"
            this.app.SetStatus("Clés chargées — OpenAI: " oaiMask "  |  DeepL " endpoint ": " dplMask, 0)
        }
    }

    ; Lit depuis config.ini [API] en priorité, puis depuis les variables d'env
    LoadKey(iniKey, envKey) {
        val := ""
        try
            val := IniRead(PipelineManager.INI_FILE, "API", iniKey, "")
        if (Trim(val) != "")
            return Trim(val)
        return Trim(EnvGet(envKey))
    }

    ; Lit un timeout depuis une variable d'environnement, retourne defaultVal si absent/invalide
    LoadTimeout(envKey, defaultVal) {
        raw := Trim(EnvGet(envKey))
        if (raw = "" || !IsInteger(raw))
            return defaultVal
        val := Integer(raw)
        return (val >= 0) ? val : defaultVal
    }

    StartTask(text, taskName) {
        if (Trim(text) = "" || this.isProcessing)
            return

        this.currentTask := taskName

        ; ── CORRECTION — OpenAI Responses API (gpt-5-nano, latence minimale) ──
        if (taskName = "Correction" || taskName = "Combined_Correction") {
            if (this.openaiKey = "") {
                this.app.SetStatus("⚠ Clé OpenAI manquante — ajoutez OPENAI_KEY dans config.ini", 0)
                return
            }
            this.app.SetStatus("Correction (gpt-5-nano, reasoning minimal)...", 30)

            systemPrompt := "Correcteur orthographique et grammatical. Corrige uniquement les erreurs sans modifier le style ni le sens. Retourne uniquement le texte corrigé, sans aucun commentaire."
            safeText := APIClient.JsonEscape(text)
            safeSystem := APIClient.JsonEscape(systemPrompt)

            ; Responses API : instructions = system, input = user text
            ; reasoning.effort "minimal" + store:false → latence réduite, pas de stockage
            ; max_output_tokens : plafond explicite pour limiter la latence
            body := '{'
                . '"model":"gpt-5-nano",'
                . '"instructions":"' safeSystem '",'
                . '"input":"' safeText '",'
                . '"reasoning":{"effort":"minimal"},'
                . '"store":false,'
                . '"max_output_tokens":2048'
                . '}'

            headers := Map(
                "Authorization", "Bearer " this.openaiKey,
                "Content-Type",  "application/json"
            )
            this.SendAsyncRequest(PipelineManager.OPENAI_URL, body, headers)
        }

        ; ── TRADUCTION — DeepL /v2/translate JSON, sans fallback ─────────────
        else if (taskName = "Translation" || taskName = "Combined_Translation") {
            if (this.deeplKey = "") {
                this.app.SetStatus("⚠ Clé DeepL manquante — ajoutez DEEPL_KEY dans config.ini", 0)
                return
            }
            this.app.SetStatus("Traduction (DeepL " (InStr(this.deeplKey, ":fx") ? "Free" : "Pro") ")...",
                (taskName = "Translation" ? 30 : 70))

            safeText := APIClient.JsonEscape(text)

            ; source_lang intentionnellement omis : DeepL peut alors activer ses modèles
            ; next-gen pour la détection automatique, ce qui est compatible avec latency_optimized
            body := '{'
                . '"text":["' safeText '"],'
                . '"target_lang":"EN-US",'
                . '"model_type":"latency_optimized"'
                . '}'

            ; Authentification uniquement via header Authorization (standard DeepL v2)
            headers := Map(
                "Authorization", "DeepL-Auth-Key " this.deeplKey,
                "Content-Type",  "application/json"
            )
            this.SendAsyncRequest(this.deeplUrl, body, headers)
        }
    }

    SendAsyncRequest(url, body, headers) {
        this.isProcessing := true
        try {
            this.http.Open("POST", url, true)
            ; Timeouts chargés depuis les variables d'environnement (ou valeurs par défaut)
            this.http.setTimeouts(this.timeouts[1], this.timeouts[2], this.timeouts[3], this.timeouts[4])
            for key, val in headers
                this.http.SetRequestHeader(key, val)
            this.http.Send(body)
            SetTimer(this.timerFn, 50)
        } catch as err {
            this.isProcessing := false
            this.app.SetStatus("Erreur réseau : " err.Message, 0)
        }
    }

    CheckAsyncState() {
        try {
            if (this.http.readyState != 4)
                return
            SetTimer(this.timerFn, 0)
            this.isProcessing := false
            status := this.http.status
            rawBody := this.http.responseText
            if (status != 200) {
                ; Extraction centralisée du message d'erreur API
                errMsg := APIClient.ExtractApiError(rawBody, status)
                this.app.SetStatus("Erreur API " status " : " errMsg, 0)
                return
            }
            if (Trim(rawBody) = "") {
                this.app.SetStatus("Erreur : réponse vide du serveur.", 0)
                return
            }
            this.ProcessResult(rawBody)
        } catch as err {
            SetTimer(this.timerFn, 0)
            this.isProcessing := false
            this.app.SetStatus("Erreur : " err.Message, 0)
        }
    }

    ProcessResult(json) {
        ; ── Résultat correction (OpenAI Responses API) ───────────────────────
        if (this.currentTask = "Correction" || this.currentTask = "Combined_Correction") {
            ; Responses API : output[].content[].type = "output_text", champ "text"
            result := APIClient.ExtractResponsesText(json)
            if (result = "") {
                ; Tentative de récupération d'un éventuel message d'erreur dans le corps 200
                errMsg := APIClient.ExtractApiError(json, 200)
                this.app.SetStatus("Réponse OpenAI invalide ou vide" (errMsg != "" ? " : " errMsg : "."), 0)
                return
            }
            this.app.outEdit.Value := result
            if (this.currentTask = "Combined_Correction")
                this.StartTask(result, "Combined_Translation")
            else
                this.app.SetStatus("Prêt.", 100)
        }

        ; ── Résultat traduction (DeepL) ───────────────────────────────────────
        else if (this.currentTask = "Translation" || this.currentTask = "Combined_Translation") {
            result := APIClient.ExtractDeepLText(json)
            if (result = "") {
                errMsg := APIClient.ExtractApiError(json, 200)
                this.app.SetStatus("Réponse DeepL invalide ou vide" (errMsg != "" ? " : " errMsg : "."), 0)
                return
            }
            this.app.outEdit.Value := result
            this.app.SetStatus("Terminé.", 100)
        }
    }
}

class APIClient {

    ; ── Échappe les caractères spéciaux JSON dans une chaîne ─────────────────
    static JsonEscape(str) {
        str := StrReplace(str, "\",   "\\")
        str := StrReplace(str, Chr(34), "\" Chr(34))
        str := StrReplace(str, "`n",  "\n")
        str := StrReplace(str, "`r",  "\r")
        str := StrReplace(str, "`t",  "\t")
        return str
    }

    ; ── Extraction générique d'une valeur string JSON ─────────────────────────
    ; Retourne la valeur du premier champ "keyName":"..." trouvé dans json.
    static ExtractJsonString(json, keyName) {
        pos := InStr(json, '"' keyName '":')
        if !pos
            return ""
        pos := pos + StrLen(keyName) + 3
        while (SubStr(json, pos, 1) ~= "\s")
            pos++
        if (SubStr(json, pos, 1) != '"')
            return ""
        pos++
        result := ""
        while (pos <= StrLen(json)) {
            ch := SubStr(json, pos, 1)
            if (ch = "\") {
                nextCh := SubStr(json, pos + 1, 1)
                if (nextCh = "n") {
                    result .= "`n"
                    pos += 2
                } else if (nextCh = '"') {
                    result .= '"'
                    pos += 2
                } else if (nextCh = "\") {
                    result .= "\"
                    pos += 2
                } else if (nextCh = "u" && pos + 5 <= StrLen(json)) {
                    hex := SubStr(json, pos + 2, 4)
                    if (RegExMatch(hex, "^[0-9A-Fa-f]{4}$")) {
                        result .= Chr(Integer("0x" hex))
                        pos += 6
                    } else {
                        result .= nextCh
                        pos += 2
                    }
                } else {
                    result .= nextCh
                    pos += 2
                }
                continue
            }
            if (ch = '"')
                break
            result .= ch
            pos++
        }
        return result
    }

    ; ── Extraction du texte depuis OpenAI Responses API ──────────────────────
    ; Structure attendue : output[].content[].type = "output_text", champ "text"
    static ExtractResponsesText(json) {
        ; Localise le premier bloc output_text, puis extrait son champ "text"
        pos := InStr(json, '"output_text"')
        if !pos
            return ""
        subJson := SubStr(json, pos)
        return APIClient.ExtractJsonString(subJson, "text")
    }

    ; ── Extraction du texte traduit depuis la réponse DeepL /v2/translate ────
    ; Structure attendue : {"translations":[{"text":"..."}]}
    static ExtractDeepLText(json) {
        ; Localise le tableau translations, puis extrait le premier champ "text"
        pos := InStr(json, '"translations"')
        if !pos
            return ""
        subJson := SubStr(json, pos)
        return APIClient.ExtractJsonString(subJson, "text")
    }

    ; ── Extraction centralisée du message d'erreur API ───────────────────────
    ; Tente les champs communs : "message" (OpenAI), "message" (DeepL),
    ; puis retourne un extrait brut en dernier recours.
    static ExtractApiError(json, httpStatus) {
        if (Trim(json) = "")
            return "réponse vide (HTTP " httpStatus ")"

        ; Champ "message" standard (OpenAI Responses API & Chat API)
        msg := APIClient.ExtractJsonString(json, "message")
        if (msg != "")
            return msg

        ; Certaines erreurs DeepL encapsulent dans un objet "error"
        posErr := InStr(json, '"error"')
        if posErr {
            subJson := SubStr(json, posErr)
            msg := APIClient.ExtractJsonString(subJson, "message")
            if (msg != "")
                return msg
        }

        ; Fallback : extrait brut des 200 premiers caractères
        return SubStr(json, 1, 200)
    }
}

global App := LuminaLexApp()
²::App.ToggleGui()
Home::App.ToggleGui()
#HotIf App.visible
Escape::App.ToggleGui()
#HotIf
