# Documentation

This directory contains documentation for BicepGuard.

## Available Documentation

### [Integration & Monitoring Guide](INTEGRATION.md)
**Start here!** Complete guide for integrating drift detection into GitHub Actions workflows, including automated monitoring setup and CI/CD integration.

### [Drift Ignore Configuration Guide](DRIFT-IGNORE.md)
**Essential for production!** Configure drift ignore rules to suppress Azure platform behaviors and false positives.

### [Docker Usage Guide](DOCKER.md)
Run BicepGuard as a Docker container with complete authentication and integration options.

### [OpenID Connect Setup](OIDC-SETUP.md)
Configure OIDC for secure, credential-free Azure authentication in GitHub Actions.

### [Release Notes](RELEASE-NOTES.md)
Version history and feature highlights for all BicepGuard releases.

## Quick Links

- **Repository**: [github.com/mwhooo/bicepguard](https://github.com/mwhooo/bicepguard)
- **Docker Hub**: [mwhooo/bicepguard](https://hub.docker.com/r/mwhooo/bicepguard)
- **Getting Started**: See main [README.md](../README.md)

## Documentation Structure

1. **Getting Started** → Main README.md
2. **CI/CD Setup** → INTEGRATION.md (includes monitoring)
3. **Fine-tuning** → DRIFT-IGNORE.md
4. **Containerization** → DOCKER.md
5. **Security** → OIDC-SETUP.md
6. **Release Info** → RELEASE-NOTES.md

## Contributing

When adding new documentation:
1. Follow the existing structure and formatting
2. Include practical examples
3. Add cross-references to related documentation
4. Update this index file
