# Predictive Health Monitor

A cross-platform monitoring and failure-prediction system developed for my MSc thesis.

The project explores whether lightweight operating-system metrics can be used to detect deteriorating test environments before they lead to failures. The monitoring service is implemented in C#/.NET 8 and supports both Windows and Linux.

It collects runtime metrics related to CPU, memory, disk and network usage and evaluates system health using rule-based heuristics and machine-learning models, including Random Forest and Logistic Regression.

## Results

Both models successfully detected elevated-risk states in a highly imbalanced dataset. Random Forest provided the best precision–recall balance, combining high recall with substantially better precision, while Logistic Regression was slightly more sensitive to elevated risk. In lead-time evaluation, Logistic Regression produced warnings approximately 33 seconds before failure on average, demonstrating that the monitored system state could provide actionable early warning rather than only detecting failures after they occurred.

## Current Development

The system is currently being integrated into an industrial test automation environment for further validation under real-world conditions.

---

Developed as part of my MSc thesis: **Predictive Health Monitoring and Adaptive Recovery in Automated Test Frameworks**.
