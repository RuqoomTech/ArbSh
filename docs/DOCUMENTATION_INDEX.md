# ArbSh Documentation Index

**Current Version:** 0.9.0
**Status:** Phase 6 In Progress - External Process and Eco Integration
**Next Phase:** Freeze `arbsh-host-v1` and implement structured process hosting

This index provides comprehensive access to all ArbSh project documentation, organized by category and current relevance.

## 📋 Core Project Documentation

| Document | Description | Status | Audience |
|----------|-------------|--------|----------|
| [README.md](../README.md) | Project overview, Arabic-first philosophy, current status | ✅ Current | All Users |
| [ROADMAP.md](../ROADMAP.md) | Development phases, completed features, upcoming work | ✅ Current | Developers, PM |
| [PROJECT_ORGANIZATION.md](PROJECT_ORGANIZATION.md) | Project structure, architecture, implementation status | ✅ Current | Developers |
| [USAGE_EXAMPLES.md](USAGE_EXAMPLES.md) | Comprehensive feature guide with working examples | ✅ Current | Users, Developers |
| [CHANGELOG.md](CHANGELOG.md) | Version history and technical implementation details | ✅ Current | All Users |
| [ARBSH_HOST_V1.md](ARBSH_HOST_V1.md) | Planned Eco host, process, PTY/ConPTY, and Qalam integration contract | 🧭 Planned | Integrators |

## 🔬 Technical Implementation Documentation

### BiDi Algorithm Design Documents

Comprehensive technical documentation for the Unicode BiDi Algorithm (UAX #9) implementation:

| Document | Description | Status | Coverage |
|----------|-------------|--------|----------|
| [BIDI_X_RULES_DESIGN.md](BIDI_X_RULES_DESIGN.md) | X Rules (X1-X10) - Explicit formatting codes | ✅ Complete | LRE, RLE, PDF, LRO, RLO, LRI, RLI, FSI, PDI |
| [BIDI_W_RULES_DESIGN.md](BIDI_W_RULES_DESIGN.md) | W Rules (W1-W7) - Weak type resolution | ✅ Complete | ES, ET, EN, AN handling |
| [BIDI_N_RULES_DESIGN.md](BIDI_N_RULES_DESIGN.md) | N Rules (N0-N2) - Neutral type resolution | ✅ Complete | Boundary neutrals, bracket pairs |
| [BIDI_I_RULES_DESIGN.md](BIDI_I_RULES_DESIGN.md) | I Rules (I1-I2) - Implicit embedding levels | ✅ Complete | Strong type level assignment |
| [BIDI_L_RULES_DESIGN.md](BIDI_L_RULES_DESIGN.md) | L Rules (L1-L4) - Level-based reordering | ✅ Complete | Combining marks, mirroring |

### Architecture Documentation

| Document | Description | Status | Focus |
|----------|-------------|--------|-------|
| [CONTRIBUTING.md](../CONTRIBUTING.md) | Development guidelines and contribution process | ✅ Current | Contributors |
| Task Management Files | Detailed implementation task tracking | ✅ Active | Project Planning |

## 📚 User Documentation

### Getting Started

| Document | Description | Status | Audience |
|----------|-------------|--------|----------|
| [USAGE_EXAMPLES.md](USAGE_EXAMPLES.md) | Complete feature guide with examples | ✅ Current | End Users |
| Installation Guide | Setup and installation instructions | 📋 Planned | New Users |
| Quick Start Guide | Essential commands and workflows | 📋 Planned | New Users |

### Feature Documentation

| Feature Area | Documentation | Status | Description |
|--------------|---------------|--------|-------------|
| **Subexpression Execution** | [USAGE_EXAMPLES.md](USAGE_EXAMPLES.md#subexpression-execution) | ✅ Complete | `$(...)` command substitution |
| **Type Literal Utilization** | [USAGE_EXAMPLES.md](USAGE_EXAMPLES.md#type-literal-utilization) | ✅ Complete | `[TypeName]` type casting |
| **Arabic Language Support** | [USAGE_EXAMPLES.md](USAGE_EXAMPLES.md#arabic-language-support) | ✅ Complete | Arabic commands and BiDi |
| **Pipeline Execution** | [USAGE_EXAMPLES.md](USAGE_EXAMPLES.md#pipeline-and-redirection) | ✅ Complete | Task-based concurrency |
| **Parameter Binding** | [USAGE_EXAMPLES.md](USAGE_EXAMPLES.md) | ✅ Complete | Reflection-based binding |

## 🎯 Documentation Roadmaps

### For New Users
1. **[README.md](../README.md)** - Project overview and Arabic-first philosophy
2. **[USAGE_EXAMPLES.md](USAGE_EXAMPLES.md)** - Complete feature guide with examples
3. **[ROADMAP.md](../ROADMAP.md)** - Understanding project direction and phases

### For Developers
1. **[PROJECT_ORGANIZATION.md](PROJECT_ORGANIZATION.md)** - Architecture and project structure
2. **BiDi Design Documents** - Technical implementation details for Unicode compliance
3. **[CHANGELOG.md](CHANGELOG.md)** - Technical implementation history
4. **Task Management Files** - Current development planning and progress

### For Contributors
1. **[CONTRIBUTING.md](../CONTRIBUTING.md)** - Development guidelines
2. **[PROJECT_ORGANIZATION.md](PROJECT_ORGANIZATION.md)** - Understanding codebase structure
3. **BiDi Design Documents** - Technical specifications for algorithm implementation

## 📋 Planned Documentation (Phase 6+)

### User Documentation
- **Installation Guide** - Setup instructions for different platforms
- **Quick Start Tutorial** - Essential workflows for new users
- **Arabic Developer Guide** - Arabic-specific features and workflows
- **Command Reference** - Comprehensive cmdlet documentation

### Technical Documentation
- **API Reference** - Generated from XML comments
- **Cmdlet Developer Guide** - Creating custom cmdlets
- **PTY/ConPTY Hosting Guide** - Interactive process and terminal integration
- **Testing Guide** - Framework and testing strategies

### Localization Documentation
- **Arabic Localization Guide** - Complete Arabic language support
- **RTL Console Implementation** - Technical details for RTL input/output
- **Cultural Adaptation Guide** - Arabic developer workflow optimization

## 📝 Documentation Maintenance

**Current Standards:**
- Maintained documents reflect version 0.9.0 status
- Technical accuracy verified against working implementation
- Examples tested with actual ArbSh shell
- Arabic-first philosophy consistently represented

**Update Process:**
- Version increments require documentation review
- New features require corresponding documentation updates
- BiDi algorithm changes require design document updates
- User-facing changes require usage example updates

## 🔍 Quick Reference

**Most Important Documents:**
1. **[README.md](../README.md)** - Start here for project overview
2. **[USAGE_EXAMPLES.md](USAGE_EXAMPLES.md)** - Complete working examples
3. **[PROJECT_ORGANIZATION.md](PROJECT_ORGANIZATION.md)** - Technical architecture
4. **[ROADMAP.md](../ROADMAP.md)** - Development phases and progress

**For Immediate Development:**
- Task management files for current work planning
- BiDi design documents for algorithm implementation
- CHANGELOG.md for technical implementation history

This comprehensive documentation index supports ArbSh's mission as an Arabic-first shell with full Unicode BiDi compliance and modern shell capabilities.
