import numpy as np
import matplotlib.pyplot as plt
from io import StringIO
from skimage.restoration import denoise_tv_chambolle

def preprocess(file, smooth = 15):
    with open("source/"+file, 'r') as file:
        file_content = file.read()
    file_content = file_content.replace(',', '.')
    file_buffer = StringIO(file_content)

    data = np.genfromtxt(file_buffer, delimiter='\t')

    # Применим Total Variation Filtering (делаем функцию более гладкой)
    # Меняем smooth зависимости от требований
    # Нужна более гладкая функци ? - smooth ставим больше
    # Нужна менее сглаженная ? - smooth ставим поменьше
    time = data[:, 0] * 1000
    voltage = denoise_tv_chambolle((data[:, 1] * 1000), smooth)
    return time, voltage

def find_action_potentials(time, voltage, alpha_threshold = 1.5, refractory_period = 100):
    # Выделенные ПД будут типа dictionary, которые хранят следующие штуки:
    # "pre_start" - нулевое или время конца предыдущего ПД
    # "start" - непосредственно начало фазы 0
    # "peak" - максимум ПД
    # "end" - минимум фазы 4
    voltage_derivative = np.diff(voltage) / np.diff(time)

    candidate_phase_0_start_indices = np.where(voltage_derivative > alpha_threshold)[0]
    phase_0_start_indices = []
    for i in range(len(candidate_phase_0_start_indices)):
        if i == 0 or candidate_phase_0_start_indices[i] - candidate_phase_0_start_indices[i-1] > refractory_period:
            phase_0_start_indices.append(candidate_phase_0_start_indices[i])

    action_potentials = []
    for i, start_index in enumerate(phase_0_start_indices):
        pre_start_index = action_potentials[-1]['end'] if i > 0 else 0

        if i < len(phase_0_start_indices) - 1:
            next_start_index = phase_0_start_indices[i + 1]
            peak_index = np.argmax(voltage[start_index:next_start_index]) + start_index
            end_index = np.argmin(voltage[peak_index:next_start_index]) + peak_index
        else:
            peak_index = np.argmax(voltage[start_index:]) + start_index
            end_index = np.argmin(voltage[peak_index:]) + peak_index

        action_potentials.append({
            'pre_start': pre_start_index,
            'start': start_index,
            'peak': peak_index,
            'end': end_index
        })

    return action_potentials

time, voltage = preprocess("1201.txt")

# Резка
action_potentials = find_action_potentials(time, voltage)

#Все ПД
plt.figure()
for ap in action_potentials:
    pre_start_index = ap['pre_start']
    end_index = ap['end']
    ap_time = time[pre_start_index:end_index+1]
    ap_voltage = voltage[pre_start_index:end_index+1]

    plt.plot(ap_time, ap_voltage)

plt.xlabel("Time")
plt.ylabel("Voltage")
plt.title("Merged Action Potentials")
plt.show()