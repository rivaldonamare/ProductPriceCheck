// Camera interop for the Scanner page. Never throws to .NET for expected failures:
// start() returns { ok, code, message } so the UI can show a specific, safe message.
window.cameraInterop = (function () {
    let stream = null;
    let dotNetRef = null;

    function stopStream() {
        if (stream) {
            stream.getTracks().forEach(function (track) {
                track.onended = null;
                track.stop();
            });
            stream = null;
        }
    }

    function mapError(error) {
        const name = error && error.name;
        switch (name) {
            case "NotAllowedError":
            case "SecurityError":
                return { code: "permission-denied", message: "Camera permission was denied. Allow camera access in the browser's site settings, then retry." };
            case "NotFoundError":
            case "OverconstrainedError":
                return { code: "no-camera", message: "No camera was found. Connect a webcam, then retry." };
            case "NotReadableError":
            case "AbortError":
                return { code: "in-use", message: "The camera is in use by another application or tab. Close it, then retry." };
            default:
                return { code: "unknown", message: "The camera could not be started. Check the connection and browser permissions, then retry." };
        }
    }

    return {
        start: async function (videoId, ref) {
            stopStream();
            dotNetRef = ref;

            if (!window.isSecureContext || !navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
                return { ok: false, code: "unsupported", message: "Camera access needs a supported browser and must be opened via http://localhost or HTTPS." };
            }

            try {
                stream = await navigator.mediaDevices.getUserMedia({
                    video: { width: { ideal: 1280 }, height: { ideal: 720 }, facingMode: { ideal: "environment" } },
                    audio: false
                });
            } catch (error) {
                return Object.assign({ ok: false }, mapError(error));
            }

            const video = document.getElementById(videoId);
            if (!video) {
                stopStream();
                return { ok: false, code: "unknown", message: "The camera preview could not be shown. Reload the page." };
            }

            // Unplugged camera / revoked permission -> tell .NET so the UI can offer a retry.
            stream.getVideoTracks().forEach(function (track) {
                track.onended = function () {
                    stopStream();
                    video.srcObject = null;
                    if (dotNetRef) {
                        dotNetRef.invokeMethodAsync("OnCameraEnded").catch(function () { });
                    }
                };
            });

            video.srcObject = stream;
            try {
                await video.play();
            } catch (error) {
                // Autoplay can be blocked; the stream is still attached and plays once the user interacts.
            }

            return { ok: true, code: null, message: null };
        },

        stop: function (videoId) {
            stopStream();
            dotNetRef = null;
            const video = document.getElementById(videoId);
            if (video) {
                video.srcObject = null;
            }
        },

        // Returns JPEG bytes (Uint8Array -> byte[] in .NET), downscaled so the longest side is <= 640 px.
        capture: async function (videoId) {
            const video = document.getElementById(videoId);
            if (!video || !video.videoWidth || !stream) {
                return null;
            }

            const maxDimension = 640;
            const scale = Math.min(1, maxDimension / Math.max(video.videoWidth, video.videoHeight));
            const canvas = document.createElement("canvas");
            canvas.width = Math.round(video.videoWidth * scale);
            canvas.height = Math.round(video.videoHeight * scale);
            canvas.getContext("2d").drawImage(video, 0, 0, canvas.width, canvas.height);

            const blob = await new Promise(function (resolve) { canvas.toBlob(resolve, "image/jpeg", 0.85); });
            if (!blob) {
                return null;
            }

            return new Uint8Array(await blob.arrayBuffer());
        }
    };
})();
