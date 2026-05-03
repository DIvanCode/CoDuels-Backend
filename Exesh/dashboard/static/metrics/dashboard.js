const state = {
  paused: false,
  timer: null,
  history: [],
  executions: [],
  workers: {},
  jobs: [],
  latestWorkers: [],
  latestWorkerPool: { registered_workers: 0, workers: [] },
};

const colors = {
  started: "#58b9d7",
  finished: "#46c278",
  picks: "#e0b64b",
  p50: "#7297ff",
  p95: "#e56767",
  slot: "#58b9d7",
  memory: "#46c278",
};

document.getElementById("pauseButton").addEventListener("click", () => {
  state.paused = !state.paused;
  document.getElementById("pauseButton").textContent = state.paused ? "Resume" : "Pause";
});

document.getElementById("clearButton").addEventListener("click", () => {
  state.history = [];
  state.executions = [];
  state.workers = {};
  state.jobs = [];
});

document.getElementById("refreshMs").addEventListener("change", schedule);

schedule();

function schedule() {
  if (state.timer) {
    clearInterval(state.timer);
  }
  const refreshMs = Math.max(250, Number(document.getElementById("refreshMs").value) || 1000);
  state.timer = setInterval(tick, refreshMs);
  tick();
}

async function tick() {
  if (state.paused) {
    return;
  }
  try {
    const response = await fetch("api/history/?minutes=30", { cache: "no-store" });
    const history = await response.json();
    consume(history);
  } catch (error) {
    document.getElementById("status").innerHTML = `<span class="bad">${escapeHtml(error.message)}</span>`;
  }
}

function consume(history) {
  state.history = history.execution || [];
  state.executions = history.executions || [];
  state.workers = history.workers || {};
  state.jobs = history.jobs || [];
  state.latestWorkers = history.latest_workers || [];
  state.latestWorkerPool = history.latest_worker_pool || { registered_workers: 0, workers: [] };

  renderStatus();
  renderKpis(state.history[state.history.length - 1]);
  renderCharts();
  renderRectangles({ jobs: state.jobs, timestamp: Date.now() / 1000 });
  renderWorkerPool(state.latestWorkerPool);
  renderWorkers(state.latestWorkers);
}

function renderStatus() {
  const last = state.history[state.history.length - 1];
  const text = last ? new Date(last.timestamp * 1000).toLocaleTimeString() : "no events";
  document.getElementById("status").innerHTML = `<span class="ok">${state.history.length} execution points</span> · ${text}`;
}

function renderKpis(point) {
  document.getElementById("startedRate").textContent = fmt(point?.started_rate || 0);
  document.getElementById("finishedRate").textContent = fmt(point?.finished_rate || 0);
  document.getElementById("pickRate").textContent = fmt(point?.scheduler_pick_rate || 0);
  document.getElementById("durationP95").textContent = `${fmt(point?.duration_p95 || 0)}s`;
  document.getElementById("priorityP95").textContent = fmt(point?.priority_p95 || 0);
  document.getElementById("workerCount").textContent = fmt(state.latestWorkerPool.registered_workers || state.latestWorkers.length || 0);
}

function renderCharts() {
  drawLines("throughputChart", [
    { name: "started/s", color: colors.started, values: state.history.map((p) => p.started_rate) },
    { name: "finished/s", color: colors.finished, values: state.history.map((p) => p.finished_rate) },
    { name: "pick/s", color: colors.picks, values: state.history.map((p) => p.scheduler_pick_rate) },
  ]);

  drawLines("fairnessChart", [
    { name: "priority p50", color: colors.p50, values: state.history.map((p) => p.priority_p50) },
    { name: "priority p95", color: colors.p95, values: state.history.map((p) => p.priority_p95) },
    { name: "progress pick p10", color: colors.picks, values: state.history.map((p) => p.progress_pick_p10) },
    { name: "progress pick p90", color: colors.started, values: state.history.map((p) => p.progress_pick_p90) },
  ]);

  drawLines("slotChart", workerSeries("slot_utilization_percent"));
  drawLines("memoryChart", workerSeries("memory_utilization_percent"));
  drawTimedLines("executionPriorityChart", executionSeries("priority"));
  drawTimedLines("executionProgressChart", executionSeries("progress_ratio"), { max: 1 });
}

function executionSeries(field) {
  return state.executions
    .filter((execution) => (execution.points || []).length)
    .map((execution, index) => ({
      name: shortExecution(execution.execution_id),
      color: palette(index),
      points: execution.points.map((point) => ({
        timestamp: point.timestamp,
        value: point[field] || 0,
      })),
    }));
}

function workerSeries(field) {
  return Object.keys(state.workers).sort().map((workerId, index) => ({
    name: shortWorker(workerId),
    color: palette(index),
    values: state.workers[workerId].map((point) => point[field] || 0),
  }));
}

function drawLines(canvasId, series) {
  drawChart(canvasId, series.map((line) => ({
    ...line,
    points: line.values.map((value, index) => ({
      timestamp: index,
      value,
    })),
  })));
}

function drawTimedLines(canvasId, series, options = {}) {
  drawChart(canvasId, series, options);
}

function drawChart(canvasId, series, options = {}) {
  const canvas = document.getElementById(canvasId);
  const rect = canvas.getBoundingClientRect();
  const dpr = window.devicePixelRatio || 1;
  canvas.width = Math.max(1, Math.floor(rect.width * dpr));
  canvas.height = Math.max(1, Math.floor(rect.height * dpr));
  const ctx = canvas.getContext("2d");
  ctx.scale(dpr, dpr);
  const width = rect.width;
  const height = rect.height;
  const pad = { left: 46, right: 16, top: 14, bottom: 34 };
  ctx.clearRect(0, 0, width, height);
  ctx.strokeStyle = "#333846";
  ctx.fillStyle = "#9299a8";
  ctx.font = "12px system-ui";

  const values = series.flatMap((line) => line.points.map((point) => point.value)).filter((value) => Number.isFinite(value));
  const timestamps = series.flatMap((line) => line.points.map((point) => point.timestamp)).filter((value) => Number.isFinite(value));
  const max = options.max ?? Math.max(1, ...values) * 1.12;
  const min = 0;
  const minTs = timestamps.length ? Math.min(...timestamps) : 0;
  const maxTs = timestamps.length ? Math.max(...timestamps) : 1;

  for (let i = 0; i <= 4; i++) {
    const y = pad.top + ((height - pad.top - pad.bottom) * i) / 4;
    ctx.beginPath();
    ctx.moveTo(pad.left, y);
    ctx.lineTo(width - pad.right, y);
    ctx.stroke();
    const label = fmt(max - ((max - min) * i) / 4);
    ctx.fillText(label, 6, y + 4);
  }

  for (const line of series) {
    if (!line.points.length) {
      continue;
    }
    ctx.strokeStyle = line.color;
    ctx.lineWidth = 2;
    ctx.beginPath();
    line.points.forEach((point, i) => {
      const x = pad.left + ((width - pad.left - pad.right) * (point.timestamp - minTs)) / Math.max(0.001, maxTs - minTs);
      const y = height - pad.bottom - ((height - pad.top - pad.bottom) * (point.value - min)) / Math.max(0.001, max - min);
      if (i === 0) {
        ctx.moveTo(x, y);
      } else {
        ctx.lineTo(x, y);
      }
    });
    ctx.stroke();
  }

  let x = pad.left;
  const legendY = height - 12;
  const legendSeries = series.slice(0, 24);
  for (const line of legendSeries) {
    ctx.fillStyle = line.color;
    ctx.fillRect(x, legendY - 8, 10, 10);
    ctx.fillStyle = "#edf0f5";
    ctx.fillText(line.name, x + 14, legendY + 1);
    x += ctx.measureText(line.name).width + 34;
  }
  if (series.length > legendSeries.length) {
    ctx.fillStyle = "#9299a8";
    ctx.fillText(`+${series.length - legendSeries.length}`, x, legendY + 1);
  }
}

function renderRectangles(snapshot) {
  const root = document.getElementById("rectangles");
  const jobs = snapshot.jobs || [];
  if (!jobs.length) {
    root.innerHTML = '<div style="padding:16px;color:#9299a8">No retained job rectangles yet.</div>';
    return;
  }
  const now = snapshot.timestamp;
  const minStart = Math.min(...jobs.map((job) => job.start_timestamp_seconds || now));
  const maxFinish = Math.max(...jobs.map((job) => positiveTime(job.finish_timestamp_seconds) || now));
  const maxMemory = Math.max(1, ...jobs.map((job) => job.memory_end_mb || job.memory_mb || 0));
  const workers = [...new Set(jobs.map((job) => job.worker_id || "unknown"))].sort();
  const width = Math.max(960, (maxFinish - minStart) * 120 + 220);
  const laneHeight = 250;
  const height = workers.length * laneHeight + 42;
  const plotLeft = 150;
  const plotTop = 28;
  const plotWidth = width - plotLeft - 24;
  const scaleX = (t) => plotLeft + ((t - minStart) / Math.max(0.001, maxFinish - minStart)) * plotWidth;
  const scaleY = (workerIndex, memory) => plotTop + workerIndex * laneHeight + (1 - memory / maxMemory) * (laneHeight - 46);

  let svg = `<svg width="${width}" height="${height}" viewBox="0 0 ${width} ${height}" role="img">`;
  svg += `<style>text{font:12px system-ui;fill:#9299a8}.axis{stroke:#333846}.rect{stroke:#101217;stroke-width:1}.label{fill:#edf0f5}</style>`;
  workers.forEach((worker, index) => {
    const y0 = plotTop + index * laneHeight;
    svg += `<text class="label" x="12" y="${y0 + 18}">${escapeHtml(shortWorker(worker))}</text>`;
    svg += `<line class="axis" x1="${plotLeft}" x2="${width - 20}" y1="${y0}" y2="${y0}"></line>`;
    svg += `<text x="96" y="${scaleY(index, maxMemory) + 4}">${fmt(maxMemory)} MB</text>`;
    svg += `<text x="110" y="${scaleY(index, 0) + 4}">0 MB</text>`;
  });

  jobs.forEach((job, index) => {
    const workerIndex = Math.max(0, workers.indexOf(job.worker_id || "unknown"));
    const start = job.start_timestamp_seconds || now;
    const finish = positiveTime(job.finish_timestamp_seconds) || now;
    const memoryStart = job.memory_start_mb || 0;
    const memoryEnd = job.memory_end_mb || job.memory_mb || 0;
    const x = scaleX(start);
    const y = scaleY(workerIndex, memoryEnd);
    const w = Math.max(2, scaleX(finish) - x);
    const h = Math.max(2, scaleY(workerIndex, memoryStart) - y);
    const fill = statusColor(job.status, index);
    const title = `${job.status || "running"} ${job.job_type || ""}\njob=${job.job_id}\nexecution=${job.execution_id}\n${fmt(finish - start)}s, ${fmt(memoryEnd - memoryStart)} MB`;
    svg += `<rect class="rect" x="${x}" y="${y}" width="${w}" height="${h}" rx="2" fill="${fill}" opacity="0.82"><title>${escapeHtml(title)}</title></rect>`;
  });
  svg += "</svg>";
  root.innerHTML = svg;
}

function renderWorkerPool(workerPool) {
  const rows = workerPool.workers || [];
  document.getElementById("workerPool").innerHTML = table(
    ["worker", "slots", "running", "memory", "used", "free", "heartbeat age"],
    rows.map((worker) => [
      shortWorker(worker.worker_id),
      fmt(worker.total_slots || 0),
      fmt(worker.running_jobs || 0),
      `${fmt(worker.total_memory_mb || 0)} MB`,
      `${fmt(worker.used_expected_memory_mb || 0)} MB`,
      `${fmt(worker.free_expected_memory_mb || 0)} MB`,
      `${fmt(worker.last_heartbeat_age_seconds || 0)}s`,
    ])
  );
}

function renderWorkers(workers) {
  document.getElementById("workers").innerHTML = table(
    ["worker", "slots", "free", "memory", "available", "running", "used"],
    (workers || []).map((worker) => [
      shortWorker(worker.worker_id),
      fmt(worker.total_slots || 0),
      fmt(worker.free_slots || 0),
      `${fmt(worker.total_memory_mb || 0)} MB`,
      `${fmt(worker.available_memory_mb || 0)} MB`,
      fmt(worker.running_jobs || 0),
      `${fmt(worker.used_memory_mb || 0)} MB`,
    ])
  );
}

function table(headers, rows) {
  if (!rows.length) {
    return '<div style="padding:12px;color:#9299a8">No data.</div>';
  }
  return `<table><thead><tr>${headers.map((h) => `<th>${escapeHtml(h)}</th>`).join("")}</tr></thead><tbody>${rows
    .map((row) => `<tr>${row.map((cell) => `<td>${escapeHtml(String(cell))}</td>`).join("")}</tr>`)
    .join("")}</tbody></table>`;
}

function positiveTime(value) {
  return value && value > 0 ? value : null;
}

function fmt(value) {
  value = Number(value) || 0;
  if (Math.abs(value) >= 1000) {
    return value.toFixed(0);
  }
  if (Math.abs(value) >= 10) {
    return value.toFixed(1);
  }
  return value.toFixed(2);
}

function shortWorker(workerId) {
  return String(workerId || "").replace("http://", "");
}

function shortExecution(executionId) {
  const value = String(executionId || "");
  return value.length > 10 ? value.slice(0, 8) : value;
}

function palette(index) {
  return ["#58b9d7", "#46c278", "#e0b64b", "#e56767", "#7297ff", "#ba7cff"][index % 6];
}

function statusColor(status, index) {
  if (status === "OK") return "#46c278";
  if (status === "TL" || status === "ML") return "#e0b64b";
  if (status) return "#e56767";
  return palette(index);
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}
