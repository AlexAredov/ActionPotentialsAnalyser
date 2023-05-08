import numpy as np
import matplotlib.pyplot as plt

def replace_commas_with_dots(file_name):
    with open(file_name, 'r') as file:
        content = file.read()
    with open(file_name, 'w') as file:
        file.write(content.replace(',', '.'))

def moving_average(data, window_size):
    return np.convolve(data, np.ones(window_size), 'valid') / window_size

file_name = '1.txt'
window_size = 350
threshold = 0.0002

# Заменяем запятые на точки
replace_commas_with_dots(file_name)

# Считываем данные из файла
data = np.loadtxt(file_name)

# Разделяем данные на координаты X и Y
x_data = data[:, 0]
y_data = data[:, 1]

# Вычисляем скользящее среднее
ma_data = moving_average(y_data, window_size)

# Определяем резко возрастающие скачки
spikes = []
for i in range(len(ma_data) - 1):
    diff = ma_data[i + 1] - ma_data[i]
    if diff > threshold:
        spikes.append((x_data[i + window_size // 2], y_data[i + window_size // 2]))

# Строим график
plt.plot(x_data, y_data, label='Соединенные точки', linestyle='-', marker='o', markersize=2, alpha=0.5)

for spike in spikes:
    plt.plot(spike[0], spike[1], 'ro', markersize=5, label='Значимый скачок')

plt.xlabel('Абсцисса')
plt.ylabel('Ордината')

# Удаляем дубликаты в легенде
handles, labels = plt.gca().get_legend_handles_labels()
by_label = dict(zip(labels, handles))
plt.legend(by_label.values(), by_label.keys())

plt.show()
