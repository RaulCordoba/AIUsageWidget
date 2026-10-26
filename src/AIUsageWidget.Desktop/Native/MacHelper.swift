import AppKit
import WebKit
import UserNotifications

// Private pipe protocol. Credentials are read from stdin, never command-line arguments or files.
struct Request: Decodable {
    let operation: String
    let sessionKey: String?
    let path: String?
    let title: String?
    let body: String?
}

final class Helper: NSObject, WKNavigationDelegate, UNUserNotificationCenterDelegate {
    private var webView: WKWebView?
    private var panel: NSPanel?
    private var completed = false

    func finish(_ data: Data? = nil) {
        DispatchQueue.main.async {
            guard !self.completed else { return }
            self.completed = true
            if let data = data {
                FileHandle.standardOutput.write(data)
                FileHandle.standardOutput.write(Data([10]))
                exit(0)
            }
            // Never print URLs, cookies, provider responses or WebKit errors.
            FileHandle.standardError.write(Data("Native operation failed.\n".utf8))
            exit(1)
        }
    }

    func start(_ request: Request) {
        DispatchQueue.main.asyncAfter(deadline: .now() + 40) { self.finish() }
        switch request.operation {
        case "self-test":
            let configuration = WKWebViewConfiguration()
            configuration.websiteDataStore = .nonPersistent()
            webView = WKWebView(frame: .zero, configuration: configuration)
            finish(Data("{\"webKit\":true}".utf8))
        case "browser":
            guard let session = request.sessionKey, !session.isEmpty,
                  let path = request.path, path.hasPrefix("/api/"),
                  let url = URL(string: "https://claude.ai" + path),
                  url.host == "claude.ai", url.scheme == "https" else { finish(); return }
            let configuration = WKWebViewConfiguration()
            configuration.websiteDataStore = .nonPersistent()
            let view = WKWebView(frame: NSRect(x: 0, y: 0, width: 800, height: 600), configuration: configuration)
            webView = view
            view.navigationDelegate = self
            let window = NSPanel(contentRect: NSRect(x: -10000, y: -10000, width: 800, height: 600),
                styleMask: .borderless, backing: .buffered, defer: false)
            window.contentView = view
            window.alphaValue = 0.01
            window.orderBack(nil)
            panel = window
            guard let cookie = HTTPCookie(properties: [.name: "sessionKey", .value: session,
                .domain: ".claude.ai", .path: "/", .secure: "TRUE", HTTPCookiePropertyKey(rawValue: "HttpOnly"): "TRUE"]) else {
                finish(); return
            }
            configuration.websiteDataStore.httpCookieStore.setCookie(cookie) { view.load(URLRequest(url: url)) }
        case "notify", "notification-permission":
            let center = UNUserNotificationCenter.current()
            center.delegate = self
            center.requestAuthorization(options: [.alert, .sound]) { allowed, error in
                guard allowed, error == nil else { self.finish(); return }
                if request.operation == "notification-permission" { self.finish(Data("{}".utf8)); return }
                let content = UNMutableNotificationContent()
                content.title = request.title ?? "AI Usage Widget"
                content.body = request.body ?? ""
                content.sound = .default
                center.add(UNNotificationRequest(identifier: UUID().uuidString, content: content, trigger: nil)) { error in
                    if error != nil { self.finish(); return }
                    // Keep the helper alive briefly so foreground delivery completes before exit.
                    DispatchQueue.main.asyncAfter(deadline: .now() + 2) { self.finish(Data("{}".utf8)) }
                }
            }
        default: finish()
        }
    }

    func webView(_ webView: WKWebView, decidePolicyFor navigationAction: WKNavigationAction,
                 decisionHandler: @escaping (WKNavigationActionPolicy) -> Void) {
        // Prevent top-level redirects from sending the browser outside Claude.
        guard navigationAction.request.url?.scheme == "https",
              let host = navigationAction.request.url?.host,
              host == "claude.ai" || host.hasSuffix(".claude.ai") else {
            decisionHandler(.cancel); finish(); return
        }
        decisionHandler(.allow)
    }

    func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
        webView.evaluateJavaScript("document.body ? document.body.innerText : ''") { value, error in
            guard error == nil, let body = value as? String, let data = body.data(using: .utf8),
                  (try? JSONSerialization.jsonObject(with: data)) != nil else { self.finish(); return }
            self.finish(data)
        }
    }
    func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) { finish() }
    func webView(_ webView: WKWebView, didFailProvisionalNavigation navigation: WKNavigation!, withError error: Error) { finish() }
    func userNotificationCenter(_ center: UNUserNotificationCenter, willPresent notification: UNNotification,
                                withCompletionHandler completionHandler: @escaping (UNNotificationPresentationOptions) -> Void) {
        completionHandler([.banner, .list, .sound])
    }
}

guard let input = readLine(), let data = input.data(using: .utf8),
      let request = try? JSONDecoder().decode(Request.self, from: data) else { exit(1) }
let application = NSApplication.shared
application.setActivationPolicy(.accessory)
let helper = Helper()
helper.start(request)
application.run()
