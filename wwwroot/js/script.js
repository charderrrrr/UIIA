let audio = null;
let isMusicPlaying = false;

function createAudioContext() {
    if (!audio) {
        audio = new Audio('/audio/uiia-cat.mp3');
        audio.loop = true;
        audio.volume = 0.4;
    }
}

function toggleMusic() {
    createAudioContext();
    const btn = document.getElementById('musicToggle');

    if (isMusicPlaying) {
        audio.pause();
        btn.textContent = 'ВКЛЮЧИТЬ МУЗЫКУ';
        isMusicPlaying = false;
    } else {
        audio.play().catch(() => {
            console.log('Автовоспроизведение заблокировано браузером. Нажмите ещё раз.');
        });
        btn.textContent = 'ВЫКЛЮЧИТЬ МУЗЫКУ';
        isMusicPlaying = true;
    }
}

function startUiiaMode() {
    createAudioContext();

    if (!isMusicPlaying) {
        audio.play().catch(() => {});
        isMusicPlaying = true;
        const btn = document.getElementById('musicToggle');
        if (btn) btn.textContent = 'ВЫКЛЮЧИТЬ МУЗЫКУ';
    }

    document.getElementById('uiiaOverlay').classList.add('active');
}

function stopUiiaMode() {
    document.getElementById('uiiaOverlay').classList.remove('active');
}

function addHeaderRow() {
    const container = document.getElementById('headersContainer');
    const row = document.createElement('div');
    row.className = 'header-row';
    row.innerHTML = `
        <input type="text" placeholder="Название заголовка">
        <input type="text" placeholder="Значение заголовка">
        <button type="button" class="btn btn-remove" onclick="removeHeaderRow(this)" title="Удалить">x</button>
    `;
    container.appendChild(row);
}

function removeHeaderRow(button) {
    const rows = document.querySelectorAll('.header-row');
    if (rows.length > 1) {
        button.parentElement.remove();
    }
}

document.getElementById('datasetFile').addEventListener('change', function(e) {
    const fileName = e.target.files[0]?.name;
    const span = document.getElementById('datasetFileName');
    if (fileName) {
        span.textContent = 'Выбран файл: ' + fileName;
    } else {
        span.textContent = '';
    }
});

document.getElementById('testForm').addEventListener('submit', async function(e) {
    e.preventDefault();

    const submitBtn = document.getElementById('submitBtn');
    const resultContainer = document.getElementById('resultContainer');

    startUiiaMode();

    submitBtn.disabled = true;
    resultContainer.classList.remove('active');

    const headers = {};
    document.querySelectorAll('.header-row').forEach(row => {
        const inputs = row.querySelectorAll('input');
        if (inputs[0].value.trim() && inputs[1].value.trim()) {
            headers[inputs[0].value.trim()] = inputs[1].value.trim();
        }
    });

    let datasetContent = '';
    const datasetFileInput = document.getElementById('datasetFile');
    if (datasetFileInput.files.length > 0) {
        const file = datasetFileInput.files[0];
        datasetContent = await new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => resolve(reader.result);
            reader.onerror = () => reject(new Error('Ошибка чтения файла датасета'));
            reader.readAsText(file, 'UTF-8');
        });
    }

    const payload = {
        target: document.getElementById('target').value,
        protocol: document.getElementById('protocol').value,
        durationSec: parseInt(document.getElementById('durationSec').value),
        mode: document.getElementById('mode').value,
        attackMode: document.getElementById('attackMode').value,
        minRps: parseInt(document.getElementById('minRps').value),
        maxRps: parseInt(document.getElementById('maxRps').value),
        timeoutMs: parseInt(document.getElementById('timeoutMs').value),
        packetSize: parseInt(document.getElementById('packetSize').value),
        packetDelayMs: parseInt(document.getElementById('packetDelayMs').value),
        headers: headers,
        datasetContent: datasetContent,
        timeScale: parseFloat(document.getElementById('timeScale').value)
    };

    try {
        const response = await fetch('/Home/StartTest', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        const data = await response.json();

        if (response.ok) {
            document.getElementById('reportLink').href = data.reportPath;
            resultContainer.classList.add('active');
        } else if (response.status === 409) {
            alert('Тест уже выполняется. Дождитесь завершения текущего теста.');
        } else {
            alert('Ошибка: ' + (data.title || 'Неизвестная ошибка'));
        }
    } catch (error) {
        alert('Ошибка запроса: ' + error.message);
    } finally {
        stopUiiaMode();
        submitBtn.disabled = false;
    }
});