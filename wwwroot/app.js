"use strict";

const state = {
    config: null,
    mode: "image",
    image: null,
    imageUrl: null,
    markdown: "",
    controller: null,
    loadingTimer: null,
    toastTimer: null
};

const elements = {
    form: document.querySelector("#translationForm"),
    sourceLanguage: document.querySelector("#sourceLanguage"),
    targetLanguage: document.querySelector("#targetLanguage"),
    provider: document.querySelector("#provider"),
    model: document.querySelector("#model"),
    providerStatus: document.querySelector("#providerStatus"),
    imageModeButton: document.querySelector("#imageModeButton"),
    textModeButton: document.querySelector("#textModeButton"),
    imagePane: document.querySelector("#imagePane"),
    textPane: document.querySelector("#textPane"),
    dropZone: document.querySelector("#dropZone"),
    dropPrompt: document.querySelector("#dropPrompt"),
    galleryInput: document.querySelector("#galleryInput"),
    cameraInput: document.querySelector("#cameraInput"),
    imagePreview: document.querySelector("#imagePreview"),
    previewImage: document.querySelector("#previewImage"),
    imageName: document.querySelector("#imageName"),
    imageMeta: document.querySelector("#imageMeta"),
    removeImageButton: document.querySelector("#removeImageButton"),
    maxImageSize: document.querySelector("#maxImageSize"),
    menuText: document.querySelector("#menuText"),
    characterCount: document.querySelector("#characterCount"),
    maxCharacterCount: document.querySelector("#maxCharacterCount"),
    formError: document.querySelector("#formError"),
    startupError: document.querySelector("#startupError"),
    translateButton: document.querySelector("#translateButton"),
    resultCard: document.querySelector("#resultCard"),
    emptyState: document.querySelector("#emptyState"),
    loadingState: document.querySelector("#loadingState"),
    loadingTitle: document.querySelector("#loadingTitle"),
    loadingMessage: document.querySelector("#loadingMessage"),
    cancelButton: document.querySelector("#cancelButton"),
    resultContent: document.querySelector("#resultContent"),
    resultMeta: document.querySelector("#resultMeta"),
    markdownPreview: document.querySelector("#markdownPreview"),
    markdownRaw: document.querySelector("#markdownRaw"),
    previewTab: document.querySelector("#previewTab"),
    rawTab: document.querySelector("#rawTab"),
    copyButton: document.querySelector("#copyButton"),
    downloadButton: document.querySelector("#downloadButton"),
    toast: document.querySelector("#toast")
};

document.addEventListener("DOMContentLoaded", initialize);

async function initialize() {
    bindEvents();

    try {
        const response = await fetch("/api/config", { headers: { Accept: "application/json" } });
        if (!response.ok) {
            throw new Error("Configurația MenuFFS nu a putut fi încărcată.");
        }

        state.config = await response.json();
        populateConfiguration();
    } catch (error) {
        elements.startupError.textContent = error.message || "Backendul MenuFFS nu este disponibil.";
        elements.startupError.hidden = false;
        elements.translateButton.disabled = true;
    }

    if ("serviceWorker" in navigator && location.protocol !== "file:") {
        navigator.serviceWorker.register("/service-worker.js").catch(() => {
            // Offline installation is optional; translation still works without it.
        });
    }
}

function bindEvents() {
    elements.imageModeButton.addEventListener("click", () => setMode("image"));
    elements.textModeButton.addEventListener("click", () => setMode("text"));
    elements.provider.addEventListener("change", updateModels);
    elements.model.addEventListener("change", updateProviderStatus);
    elements.menuText.addEventListener("input", updateCharacterCount);
    elements.galleryInput.addEventListener("change", event => selectFile(event.target.files?.[0]));
    elements.cameraInput.addEventListener("change", event => selectFile(event.target.files?.[0]));
    elements.removeImageButton.addEventListener("click", event => {
        event.stopPropagation();
        clearImage();
    });

    elements.dropZone.addEventListener("click", event => {
        if (state.image || event.target.closest("label, button")) {
            return;
        }
        elements.galleryInput.click();
    });

    for (const eventName of ["dragenter", "dragover"]) {
        elements.dropZone.addEventListener(eventName, event => {
            event.preventDefault();
            elements.dropZone.classList.add("dragging");
        });
    }

    for (const eventName of ["dragleave", "drop"]) {
        elements.dropZone.addEventListener(eventName, event => {
            event.preventDefault();
            elements.dropZone.classList.remove("dragging");
        });
    }

    elements.dropZone.addEventListener("drop", event => selectFile(event.dataTransfer?.files?.[0]));
    elements.form.addEventListener("submit", translateMenu);
    elements.cancelButton.addEventListener("click", cancelTranslation);
    elements.previewTab.addEventListener("click", () => setResultTab("preview"));
    elements.rawTab.addEventListener("click", () => setResultTab("raw"));
    elements.copyButton.addEventListener("click", copyMarkdown);
    elements.downloadButton.addEventListener("click", downloadMarkdown);
}

function populateConfiguration() {
    populateSelect(elements.sourceLanguage, state.config.sourceLanguages, "auto");
    populateSelect(elements.targetLanguage, state.config.targetLanguages, "ro");

    elements.provider.innerHTML = "";
    for (const provider of state.config.providers) {
        const option = document.createElement("option");
        option.value = provider.id;
        option.textContent = provider.configured
            ? provider.name
            : `${provider.name} · cheie lipsă`;
        elements.provider.append(option);
    }

    const maxImageMegabytes = state.config.maxImageBytes / 1024 / 1024;
    elements.maxImageSize.textContent = `${formatNumber(maxImageMegabytes)} MB`;
    elements.menuText.maxLength = state.config.maxMenuTextCharacters;
    elements.maxCharacterCount.textContent = formatInteger(state.config.maxMenuTextCharacters);

    if (!state.config.providers.length) {
        elements.provider.innerHTML = '<option value="">Niciun furnizor configurat</option>';
        elements.model.innerHTML = '<option value="">Niciun model disponibil</option>';
        elements.translateButton.disabled = true;
        showFormError("Nu există furnizori AI activați în configurația backendului.");
        return;
    }

    updateModels();
    updateCharacterCount();
}

function populateSelect(select, items, preferredValue) {
    select.innerHTML = "";
    for (const item of items) {
        const option = document.createElement("option");
        option.value = item.code;
        option.textContent = item.name;
        select.append(option);
    }

    if (items.some(item => item.code === preferredValue)) {
        select.value = preferredValue;
    }
}

function updateModels() {
    const provider = getSelectedProvider();
    const previousModel = elements.model.value;
    elements.model.innerHTML = "";

    if (!provider?.models?.length) {
        const option = document.createElement("option");
        option.value = "";
        option.textContent = "Niciun model configurat";
        elements.model.append(option);
        elements.model.disabled = true;
        updateProviderStatus();
        return;
    }

    elements.model.disabled = false;
    for (const model of provider.models) {
        const option = document.createElement("option");
        option.value = model.id;
        option.textContent = model.supportsVision ? model.name : `${model.name} · doar text`;
        elements.model.append(option);
    }

    if (provider.models.some(model => model.id === previousModel)) {
        elements.model.value = previousModel;
    }

    updateProviderStatus();
}

function updateProviderStatus() {
    const provider = getSelectedProvider();
    const model = getSelectedModel();
    elements.providerStatus.replaceChildren();

    if (!provider) {
        return;
    }

    const status = document.createElement("span");
    status.className = provider.configured ? "configured" : "not-configured";
    status.textContent = provider.configured
        ? `${provider.name} este configurat${model ? ` · ${model.supportsVision ? "imagine + text" : "doar text"}` : ""}`
        : `Cheia pentru ${provider.name} trebuie adăugată pe server`;
    elements.providerStatus.append(status);
}

function setMode(mode) {
    state.mode = mode;
    const imageMode = mode === "image";

    elements.imageModeButton.classList.toggle("active", imageMode);
    elements.imageModeButton.setAttribute("aria-selected", String(imageMode));
    elements.textModeButton.classList.toggle("active", !imageMode);
    elements.textModeButton.setAttribute("aria-selected", String(!imageMode));
    elements.imagePane.hidden = !imageMode;
    elements.textPane.hidden = imageMode;
    hideFormError();
}

function selectFile(file) {
    if (!file) {
        return;
    }

    hideFormError();
    const allowedTypes = new Set(["image/jpeg", "image/png", "image/webp"]);

    if (!allowedTypes.has(file.type)) {
        showFormError("Format neacceptat. Alege o imagine JPEG, PNG sau WebP.");
        resetFileInputs();
        return;
    }

    if (state.config && file.size > state.config.maxImageBytes) {
        showFormError(`Imaginea este prea mare. Limita este ${elements.maxImageSize.textContent}.`);
        resetFileInputs();
        return;
    }

    clearImage(false);
    state.image = file;
    state.imageUrl = URL.createObjectURL(file);
    elements.previewImage.src = state.imageUrl;
    elements.imageName.textContent = file.name || "Fotografie meniu";
    elements.imageMeta.textContent = `${file.type.replace("image/", "").toUpperCase()} · ${formatBytes(file.size)}`;
    elements.dropPrompt.hidden = true;
    elements.imagePreview.hidden = false;
}

function clearImage(resetInputs = true) {
    if (state.imageUrl) {
        URL.revokeObjectURL(state.imageUrl);
    }

    state.image = null;
    state.imageUrl = null;
    elements.previewImage.removeAttribute("src");
    elements.imagePreview.hidden = true;
    elements.dropPrompt.hidden = false;

    if (resetInputs) {
        resetFileInputs();
    }
}

function resetFileInputs() {
    elements.galleryInput.value = "";
    elements.cameraInput.value = "";
}

function updateCharacterCount() {
    elements.characterCount.textContent = formatInteger(elements.menuText.value.length);
}

async function translateMenu(event) {
    event.preventDefault();
    hideFormError();

    if (!state.config) {
        showFormError("Configurația aplicației nu este încă disponibilă.");
        return;
    }

    const provider = getSelectedProvider();
    const model = getSelectedModel();

    if (!provider || !model) {
        showFormError("Selectează un furnizor și un model AI.");
        return;
    }

    if (!provider.configured) {
        showFormError(`Cheia API pentru ${provider.name} nu este configurată în backend.`);
        return;
    }

    if (state.mode === "image") {
        if (!state.image) {
            showFormError("Încarcă sau fotografiază meniul înainte de traducere.");
            return;
        }

        if (!model.supportsVision) {
            showFormError(`Modelul ${model.name} nu acceptă imagini. Alege un model multimodal sau introdu textul manual.`);
            return;
        }
    } else if (!elements.menuText.value.trim()) {
        showFormError("Introdu sau lipește textul meniului înainte de traducere.");
        elements.menuText.focus();
        return;
    }

    const formData = new FormData();
    formData.append("providerId", provider.id);
    formData.append("model", model.id);
    formData.append("sourceLanguage", elements.sourceLanguage.value);
    formData.append("targetLanguage", elements.targetLanguage.value);

    if (state.mode === "image") {
        formData.append("image", state.image, state.image.name || "menu.jpg");
    } else {
        formData.append("menuText", elements.menuText.value.trim());
    }

    state.controller = new AbortController();
    showLoading(provider, model);

    try {
        const response = await fetch("/api/translate", {
            method: "POST",
            body: formData,
            signal: state.controller.signal,
            headers: { Accept: "application/json" }
        });

        const payload = await readJsonSafely(response);
        if (!response.ok) {
            throw new Error(payload?.detail || payload?.title || `Cererea a eșuat cu statusul ${response.status}.`);
        }

        showResult(payload);
    } catch (error) {
        if (error.name === "AbortError") {
            showEmptyState();
            showFormError("Cererea către model a fost oprită.");
        } else {
            showEmptyState();
            showFormError(error.message || "Traducerea nu a putut fi realizată.");
        }
    } finally {
        stopLoadingTimer();
        state.controller = null;
        elements.translateButton.disabled = false;
        elements.form.removeAttribute("aria-busy");
    }
}

function showLoading(provider, model) {
    elements.emptyState.hidden = true;
    elements.resultContent.hidden = true;
    elements.loadingState.hidden = false;
    elements.translateButton.disabled = true;
    elements.form.setAttribute("aria-busy", "true");
    elements.loadingTitle.textContent = state.mode === "image" ? "Analizează fotografia…" : "Traduce textul…";
    elements.loadingMessage.textContent = `${provider.name} · ${model.name}`;

    const phases = state.mode === "image"
        ? ["Modelul citește direct imaginea.", "Organizează preparatele și categoriile.", "Păstrează prețurile și formatează Markdown-ul."]
        : ["Modelul interpretează textul introdus.", "Traduce denumirile și descrierile.", "Formatează rezultatul în Markdown."];

    let phase = 0;
    stopLoadingTimer();
    state.loadingTimer = window.setInterval(() => {
        elements.loadingMessage.textContent = phases[phase % phases.length];
        phase += 1;
    }, 3500);

    scrollResultIntoViewOnSmallScreens();
}

function showResult(result) {
    state.markdown = result.markdown || "";
    elements.markdownPreview.innerHTML = renderMarkdown(state.markdown);
    elements.markdownRaw.textContent = state.markdown;
    elements.resultMeta.replaceChildren();

    const metadata = [
        result.providerName,
        result.model,
        result.inputMode === "image" ? "Imagine directă" : "Text manual",
        formatDuration(result.durationMilliseconds),
        "Fără fallback"
    ];

    for (const value of metadata) {
        const badge = document.createElement("span");
        badge.textContent = value;
        elements.resultMeta.append(badge);
    }

    elements.emptyState.hidden = true;
    elements.loadingState.hidden = true;
    elements.resultContent.hidden = false;
    setResultTab("preview");
    scrollResultIntoViewOnSmallScreens();
}

function showEmptyState() {
    elements.loadingState.hidden = true;
    elements.resultContent.hidden = true;
    elements.emptyState.hidden = false;
}

function cancelTranslation() {
    state.controller?.abort();
}

function stopLoadingTimer() {
    if (state.loadingTimer) {
        window.clearInterval(state.loadingTimer);
        state.loadingTimer = null;
    }
}

function setResultTab(tab) {
    const preview = tab === "preview";
    elements.previewTab.classList.toggle("active", preview);
    elements.previewTab.setAttribute("aria-selected", String(preview));
    elements.rawTab.classList.toggle("active", !preview);
    elements.rawTab.setAttribute("aria-selected", String(!preview));
    elements.markdownPreview.hidden = !preview;
    elements.markdownRaw.hidden = preview;
}

async function copyMarkdown() {
    if (!state.markdown) {
        return;
    }

    try {
        await navigator.clipboard.writeText(state.markdown);
        showToast("Markdown copiat");
    } catch {
        const selection = window.getSelection();
        const range = document.createRange();
        range.selectNodeContents(elements.markdownRaw);
        selection.removeAllRanges();
        selection.addRange(range);
        showToast("Selectează și copiază textul Markdown");
    }
}

function downloadMarkdown() {
    if (!state.markdown) {
        return;
    }

    const date = new Date().toISOString().slice(0, 10);
    const blob = new Blob([state.markdown], { type: "text/markdown;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `MenuFFS-${date}.md`;
    document.body.append(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
    showToast("Fișier Markdown descărcat");
}

function getSelectedProvider() {
    return state.config?.providers.find(provider => provider.id === elements.provider.value) || null;
}

function getSelectedModel() {
    return getSelectedProvider()?.models.find(model => model.id === elements.model.value) || null;
}

function showFormError(message) {
    elements.formError.textContent = message;
    elements.formError.hidden = false;
    elements.formError.scrollIntoView({ behavior: "smooth", block: "nearest" });
}

function hideFormError() {
    elements.formError.hidden = true;
    elements.formError.textContent = "";
}

function showToast(message) {
    window.clearTimeout(state.toastTimer);
    elements.toast.textContent = message;
    elements.toast.hidden = false;
    state.toastTimer = window.setTimeout(() => {
        elements.toast.hidden = true;
    }, 2400);
}

function scrollResultIntoViewOnSmallScreens() {
    if (window.matchMedia("(max-width: 1080px)").matches) {
        elements.resultCard.scrollIntoView({ behavior: "smooth", block: "start" });
    }
}

async function readJsonSafely(response) {
    try {
        return await response.json();
    } catch {
        return null;
    }
}

function formatBytes(bytes) {
    if (bytes < 1024) {
        return `${bytes} B`;
    }
    if (bytes < 1024 * 1024) {
        return `${formatNumber(bytes / 1024)} KB`;
    }
    return `${formatNumber(bytes / 1024 / 1024)} MB`;
}

function formatDuration(milliseconds) {
    if (milliseconds < 1000) {
        return `${milliseconds} ms`;
    }
    return `${formatNumber(milliseconds / 1000)} s`;
}

function formatNumber(value) {
    return new Intl.NumberFormat("ro-RO", { maximumFractionDigits: 1 }).format(value);
}

function formatInteger(value) {
    return new Intl.NumberFormat("ro-RO", { maximumFractionDigits: 0 }).format(value);
}

function renderMarkdown(markdown) {
    const lines = markdown.replace(/\r\n?/g, "\n").split("\n");
    const html = [];
    let index = 0;
    let openList = null;

    const closeList = () => {
        if (openList) {
            html.push(`</${openList}>`);
            openList = null;
        }
    };

    while (index < lines.length) {
        const line = lines[index];
        const trimmed = line.trim();

        if (!trimmed) {
            closeList();
            index += 1;
            continue;
        }

        if (trimmed.startsWith("```")) {
            closeList();
            const code = [];
            index += 1;
            while (index < lines.length && !lines[index].trim().startsWith("```")) {
                code.push(lines[index]);
                index += 1;
            }
            index += 1;
            html.push(`<pre><code>${escapeHtml(code.join("\n"))}</code></pre>`);
            continue;
        }

        const heading = /^(#{1,6})\s+(.+)$/.exec(trimmed);
        if (heading) {
            closeList();
            const level = heading[1].length;
            html.push(`<h${level}>${formatInline(heading[2])}</h${level}>`);
            index += 1;
            continue;
        }

        if (/^(\*{3,}|-{3,}|_{3,})$/.test(trimmed)) {
            closeList();
            html.push("<hr>");
            index += 1;
            continue;
        }

        if (isTableStart(lines, index)) {
            closeList();
            const headers = splitTableRow(lines[index]);
            index += 2;
            const rows = [];
            while (index < lines.length && lines[index].includes("|") && lines[index].trim()) {
                rows.push(splitTableRow(lines[index]));
                index += 1;
            }

            html.push('<div class="table-wrap"><table><thead><tr>');
            for (const header of headers) {
                html.push(`<th>${formatInline(header)}</th>`);
            }
            html.push("</tr></thead><tbody>");
            for (const row of rows) {
                html.push("<tr>");
                for (let cell = 0; cell < headers.length; cell += 1) {
                    html.push(`<td>${formatInline(row[cell] || "")}</td>`);
                }
                html.push("</tr>");
            }
            html.push("</tbody></table></div>");
            continue;
        }

        const unordered = /^[-*+]\s+(.+)$/.exec(trimmed);
        const ordered = /^\d+[.)]\s+(.+)$/.exec(trimmed);
        if (unordered || ordered) {
            const listType = unordered ? "ul" : "ol";
            if (openList !== listType) {
                closeList();
                openList = listType;
                html.push(`<${listType}>`);
            }
            html.push(`<li>${formatInline((unordered || ordered)[1])}</li>`);
            index += 1;
            continue;
        }

        closeList();

        if (trimmed.startsWith(">")) {
            const quote = [];
            while (index < lines.length && lines[index].trim().startsWith(">")) {
                quote.push(lines[index].trim().replace(/^>\s?/, ""));
                index += 1;
            }
            html.push(`<blockquote><p>${quote.map(formatInline).join("<br>")}</p></blockquote>`);
            continue;
        }

        const paragraph = [trimmed];
        index += 1;
        while (index < lines.length && isParagraphContinuation(lines, index)) {
            paragraph.push(lines[index].trim());
            index += 1;
        }
        html.push(`<p>${paragraph.map(formatInline).join("<br>")}</p>`);
    }

    closeList();
    return html.join("");
}

function isParagraphContinuation(lines, index) {
    const value = lines[index].trim();
    if (!value) {
        return false;
    }

    return !value.startsWith("#")
        && !value.startsWith(">")
        && !value.startsWith("```")
        && !/^[-*+]\s+/.test(value)
        && !/^\d+[.)]\s+/.test(value)
        && !/^(\*{3,}|-{3,}|_{3,})$/.test(value)
        && !isTableStart(lines, index);
}

function isTableStart(lines, index) {
    if (index + 1 >= lines.length || !lines[index].includes("|")) {
        return false;
    }

    const separators = splitTableRow(lines[index + 1]);
    return separators.length > 0 && separators.every(cell => /^:?-{3,}:?$/.test(cell.trim()));
}

function splitTableRow(line) {
    return line.trim().replace(/^\|/, "").replace(/\|$/, "").split("|").map(cell => cell.trim());
}

function formatInline(value) {
    const codeSegments = [];
    let output = escapeHtml(value).replace(/`([^`]+)`/g, (_, code) => {
        const marker = `\u0000CODE${codeSegments.length}\u0000`;
        codeSegments.push(`<code>${code}</code>`);
        return marker;
    });

    output = output
        .replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>")
        .replace(/__([^_]+)__/g, "<strong>$1</strong>")
        .replace(/(^|[^*])\*([^*]+)\*(?!\*)/g, "$1<em>$2</em>")
        .replace(/(^|[^_])_([^_]+)_(?!_)/g, "$1<em>$2</em>");

    codeSegments.forEach((code, index) => {
        output = output.replace(`\u0000CODE${index}\u0000`, code);
    });

    return output;
}

function escapeHtml(value) {
    return String(value)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}
