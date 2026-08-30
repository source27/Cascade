import { createReadStream, createWriteStream } from 'node:fs';
import { mkdir, rename, rm, stat } from 'node:fs/promises';
import { createServer } from 'node:http';
import { basename, dirname, relative, resolve, sep } from 'node:path';
import { pipeline } from 'node:stream/promises';
import { fileURLToPath } from 'node:url';
import { timingSafeEqual } from 'node:crypto';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const options = parseArguments(process.argv.slice(2));
const root = resolve(options.root ?? resolve(scriptDirectory, 'root'));
const host = options.host ?? '127.0.0.1';
const port = Number(options.port ?? 2727);
const csrfToken = options.csrf ?? '227e24ff-63a2-4499-b1f4-7fd5f0c7330f';

await mkdir(root, { recursive: true });

const server = createServer(async (request, response) => {
  try {
    await handleRequest(request, response);
  } catch (error) {
    console.error(error);
    if (!response.headersSent) {
      sendText(response, error.statusCode ?? 500, error.message ?? 'Internal Server Error');
    } else {
      response.destroy();
    }
  }
});

server.listen(port, host, () => {
  console.log(`Root: ${root}`);
  console.log(`Address: http://${host}:${port}`);
  console.log('Upload: PUT enabled, directories created automatically');
});

async function handleRequest(request, response) {
  const url = new URL(request.url, `http://${request.headers.host ?? `${host}:${port}`}`);
  const target = resolveTarget(url.pathname);

  if (request.method === 'OPTIONS') {
    response.writeHead(204, {
      Allow: 'GET, HEAD, PUT, OPTIONS',
      'Access-Control-Allow-Headers': 'Content-Type, X-CSRF-Token',
      'Access-Control-Allow-Methods': 'GET, HEAD, PUT, OPTIONS',
      'Access-Control-Allow-Origin': '*',
    });
    response.end();
    return;
  }

  if (request.method === 'PUT') {
    await uploadFile(request, response, target);
    return;
  }

  if (request.method !== 'GET' && request.method !== 'HEAD') {
    response.writeHead(405, { Allow: 'GET, HEAD, PUT, OPTIONS' });
    response.end();
    return;
  }

  let targetStat;
  try {
    targetStat = await stat(target);
  } catch (error) {
    if (error.code === 'ENOENT') {
      sendText(response, 404, 'Not Found');
      return;
    }
    throw error;
  }

  if (targetStat.isDirectory()) {
    await serveDirectory(request, response, target, url.pathname);
  } else if (targetStat.isFile()) {
    serveFile(request, response, target, targetStat);
  } else {
    sendText(response, 404, 'Not Found');
  }
}

async function uploadFile(request, response, target) {
  if (!safeTokenEquals(request.headers['x-csrf-token'] ?? '', csrfToken)) {
    sendText(response, 403, 'Invalid CSRF token');
    request.resume();
    return;
  }

  if (target === root) {
    sendText(response, 400, 'PUT target must include a filename');
    request.resume();
    return;
  }

  await mkdir(dirname(target), { recursive: true });
  const temporary = resolve(dirname(target), `.${basename(target)}.${process.pid}.${Date.now()}.uploading`);

  try {
    await pipeline(request, createWriteStream(temporary, { flags: 'wx' }));
    try {
      await rename(temporary, target);
    } catch (error) {
      if (error.code !== 'EEXIST' && error.code !== 'EPERM') {
        throw error;
      }
      await rm(target, { force: true });
      await rename(temporary, target);
    }
  } catch (error) {
    await rm(temporary, { force: true });
    throw error;
  }

  response.writeHead(201, {
    'Access-Control-Allow-Origin': '*',
    'Content-Type': 'application/json; charset=utf-8',
  });
  response.end(JSON.stringify({ path: `/${relative(root, target).split(sep).join('/')}` }));
}

async function serveDirectory(request, response, directory, pathname) {
  const { readdir } = await import('node:fs/promises');
  const entries = await readdir(directory, { withFileTypes: true });
  entries.sort((left, right) => {
    if (left.isDirectory() !== right.isDirectory()) {
      return left.isDirectory() ? -1 : 1;
    }
    return left.name.localeCompare(right.name);
  });

  const basePath = pathname.endsWith('/') ? pathname : `${pathname}/`;
  const rows = entries.map((entry) => {
    const suffix = entry.isDirectory() ? '/' : '';
    const href = `${basePath}${encodeURIComponent(entry.name)}${suffix}`;
    return `<li><a href="${escapeHtml(href)}">${escapeHtml(entry.name + suffix)}</a></li>`;
  }).join('');
  const body = Buffer.from(
    `<!doctype html><html><head><meta charset="utf-8"><title>DevCDN</title></head>` +
    `<body><h1>DevCDN ${escapeHtml(pathname)}</h1><ul>${rows}</ul></body></html>`,
  );

  response.writeHead(200, {
    'Access-Control-Allow-Origin': '*',
    'Content-Length': body.length,
    'Content-Type': 'text/html; charset=utf-8',
  });
  if (request.method === 'HEAD') {
    response.end();
  } else {
    response.end(body);
  }
}

function serveFile(request, response, target, targetStat) {
  const etag = `W/\"${targetStat.size.toString(16)}-${Math.trunc(targetStat.mtimeMs).toString(16)}\"`;
  if (request.headers['if-none-match'] === etag) {
    response.writeHead(304, { ETag: etag });
    response.end();
    return;
  }

  const range = parseRange(request.headers.range, targetStat.size);
  if (range === false) {
    response.writeHead(416, { 'Content-Range': `bytes */${targetStat.size}` });
    response.end();
    return;
  }

  const start = range?.start ?? 0;
  const end = range?.end ?? targetStat.size - 1;
  const contentLength = targetStat.size === 0 ? 0 : end - start + 1;
  const headers = {
    'Accept-Ranges': 'bytes',
    'Access-Control-Allow-Origin': '*',
    'Content-Length': contentLength,
    'Content-Type': contentType(target),
    ETag: etag,
    'Last-Modified': targetStat.mtime.toUTCString(),
  };
  if (range) {
    headers['Content-Range'] = `bytes ${start}-${end}/${targetStat.size}`;
  }

  response.writeHead(range ? 206 : 200, headers);
  if (request.method === 'HEAD' || targetStat.size === 0) {
    response.end();
    return;
  }
  createReadStream(target, { start, end }).pipe(response);
}

function resolveTarget(pathname) {
  let decoded;
  try {
    decoded = decodeURIComponent(pathname);
  } catch {
    throw httpError(400, 'Invalid URL encoding');
  }

  const target = resolve(root, decoded.replace(/^[/\\]+/, ''));
  const normalizedRoot = root.toLowerCase();
  const normalizedTarget = target.toLowerCase();
  if (normalizedTarget !== normalizedRoot && !normalizedTarget.startsWith(`${normalizedRoot}${sep}`)) {
    throw httpError(403, 'Path escapes the CDN root');
  }
  return target;
}

function parseRange(value, size) {
  if (!value) {
    return null;
  }
  const match = /^bytes=(\d*)-(\d*)$/.exec(value);
  if (!match || size === 0) {
    return false;
  }

  let start;
  let end;
  if (match[1] === '') {
    const suffixLength = Number(match[2]);
    if (!Number.isSafeInteger(suffixLength) || suffixLength <= 0) {
      return false;
    }
    start = Math.max(0, size - suffixLength);
    end = size - 1;
  } else {
    start = Number(match[1]);
    end = match[2] === '' ? size - 1 : Number(match[2]);
  }

  if (!Number.isSafeInteger(start) || !Number.isSafeInteger(end) || start < 0 || start >= size || end < start) {
    return false;
  }
  return { start, end: Math.min(end, size - 1) };
}

function safeTokenEquals(left, right) {
  const leftBuffer = Buffer.from(String(left));
  const rightBuffer = Buffer.from(String(right));
  return leftBuffer.length === rightBuffer.length && timingSafeEqual(leftBuffer, rightBuffer);
}

function contentType(path) {
  if (path.endsWith('.html')) return 'text/html; charset=utf-8';
  if (path.endsWith('.json')) return 'application/json; charset=utf-8';
  if (path.endsWith('.version') || path.endsWith('.hash')) return 'text/plain; charset=utf-8';
  return 'application/octet-stream';
}

function sendText(response, statusCode, message) {
  const body = Buffer.from(message);
  response.writeHead(statusCode, {
    'Access-Control-Allow-Origin': '*',
    'Content-Length': body.length,
    'Content-Type': 'text/plain; charset=utf-8',
  });
  response.end(body);
}

function httpError(statusCode, message) {
  const error = new Error(message);
  error.statusCode = statusCode;
  return error;
}

function escapeHtml(value) {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

function parseArguments(args) {
  const parsed = {};
  for (let index = 0; index < args.length; index += 1) {
    const argument = args[index];
    if (!argument.startsWith('--')) {
      throw new Error(`Unknown argument: ${argument}`);
    }
    const name = argument.slice(2);
    const value = args[index + 1];
    if (!value || value.startsWith('--')) {
      throw new Error(`Missing value for --${name}`);
    }
    parsed[name] = value;
    index += 1;
  }
  return parsed;
}
